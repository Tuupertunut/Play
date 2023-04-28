﻿using System;
using System.Collections.Generic;
using System.Linq;
using UniInject;
using UniRx;
using UnityEngine;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

// Takes the analyzed beats of the PlayerPitchTracker and calculates the player's score.
public class PlayerScoreControl : MonoBehaviour, INeedInjection, IInjectionFinishedListener
{
    // A total of 10000 points can be achieved.
    // Golden notes give double points.
    // Singing perfect lines gives a bonus of up to 1000 points.
    public static readonly int maxScore = 10000;
    public static readonly int maxPerfectSentenceBonusScore = 1000;
    public static readonly int maxScoreForNotes = maxScore - maxPerfectSentenceBonusScore;

    public int TotalScore
    {
        get
        {
            int calculatedTotalScore = NormalNotesTotalScore + GoldenNotesTotalScore + PerfectSentenceBonusTotalScore;
            if (calculatedTotalScore > maxScore)
            {
                Debug.LogWarning($"Total score is {calculatedTotalScore}, returning max score of {maxScore} instead. "
                                 + $"(NormalNotesTotalScore: {NormalNotesTotalScore}, GoldenNotesTotalScore: {GoldenNotesTotalScore}, PerfectSentenceBonusTotalScore: {PerfectSentenceBonusTotalScore})");
                return maxScore;
            }

            return calculatedTotalScore;
        }
    }

    public int NormalNotesTotalScore
    {
        get
        {
            if (ScoreData.NormalBeatData.PerfectAndGoodBeats <= 0
                || ScoreData.NormalNoteLengthTotal <= 0)
            {
                return 0;
            }
            return (int)(maxScoreForNormalNotes * ScoreData.NormalBeatData.PerfectAndGoodBeats / ScoreData.NormalNoteLengthTotal);
        }
    }

    public int GoldenNotesTotalScore
    {
        get
        {
            if (ScoreData.GoldenBeatData.PerfectAndGoodBeats <= 0
                || ScoreData.GoldenNoteLengthTotal <= 0)
            {
                return 0;
            }
            return (int)(maxScoreForGoldenNotes * ScoreData.GoldenBeatData.PerfectAndGoodBeats / ScoreData.GoldenNoteLengthTotal);
        }
    }

    public int PerfectSentenceBonusTotalScore
    {
        get
        {
            int targetSentenceCount = ScoreData.TotalSentenceCount > 20
                ? 20
                : ScoreData.TotalSentenceCount;
            if (targetSentenceCount <= 0)
            {
                return 0;
            }
            double score = (double)maxPerfectSentenceBonusScore * ScoreData.PerfectSentenceCount / targetSentenceCount;

            // Round the score up
            score = Math.Ceiling(score);
            if (score > maxPerfectSentenceBonusScore)
            {
                score = maxPerfectSentenceBonusScore;
            }
            return (int)score;
        }
    }

    public int NextBeatToScore { get; set; }

    [Inject]
    private PlayerMicPitchTracker playerMicPitchTracker;

    [Inject]
    private Voice voice;

    private readonly Subject<SentenceScoreEvent> sentenceScoreEventStream = new();
    public IObservable<SentenceScoreEvent> SentenceScoreEventStream
    {
        get
        {
            return sentenceScoreEventStream;
        }
    }

    private readonly Subject<NoteScoreEvent> noteScoreEventStream = new();
    public IObservable<NoteScoreEvent> NoteScoreEventStream
    {
        get
        {
            return noteScoreEventStream;
        }
    }

    private double maxScoreForNormalNotes;
    private double maxScoreForGoldenNotes;

    public PlayerScoreControlData ScoreData { get; set; } = new();

    private readonly HashSet<int> scoredBeats = new();
    private readonly HashSet<int> normalNoteBeats = new();
    private readonly HashSet<int> goldenNoteBeats = new();
    private int firstBeatToScoreInclusive;
    private int lastBeatToScoreExclusive;
    
    public void OnInjectionFinished()
    {
        UpdateMaxScores(voice.Sentences);

        playerMicPitchTracker.BeatAnalyzedEventStream.Subscribe(OnBeatAnalyzed);
        playerMicPitchTracker.NoteAnalyzedEventStream.Subscribe(OnNoteAnalyzed);
        playerMicPitchTracker.SentenceAnalyzedEventStream.Subscribe(OnSentenceAnalyzed);
    }

    private void OnBeatAnalyzed(BeatAnalyzedEvent beatAnalyzedEvent)
    {
        // Check if pitch was detected where a note is expected in the song
        if (beatAnalyzedEvent.PitchEvent == null
            || beatAnalyzedEvent.NoteAtBeat == null)
        {
            return;
        }

        if (beatAnalyzedEvent.Beat < NextBeatToScore)
        {
            return;
        }

        Note analyzedNote = beatAnalyzedEvent.NoteAtBeat;

        // Check if note was hit
        if (MidiUtils.GetRelativePitch(beatAnalyzedEvent.RoundedRecordedMidiNote) != MidiUtils.GetRelativePitch(analyzedNote.MidiNote))
        {
            return;
        }

        // The beat was sung correctly.
        if (!ScoreData.NoteToNoteScoreMap.TryGetValue(analyzedNote, out NoteScore noteScore))
        {
            noteScore = new NoteScore(analyzedNote);
            ScoreData.NoteToNoteScoreMap.Add(analyzedNote, noteScore);
        }
        noteScore.CorrectlySungBeats++;

        Sentence analyzedSentence = beatAnalyzedEvent.NoteAtBeat.Sentence;
        if (!ScoreData.SentenceToSentenceScoreMap.TryGetValue(analyzedSentence, out SentenceScore sentenceScore))
        {
            sentenceScore = CreateSentenceScore(analyzedSentence);
            ScoreData.SentenceToSentenceScoreMap.Add(analyzedSentence, sentenceScore);
        }

        if (IsPerfectHit(beatAnalyzedEvent))
        {
            ScoreData.GetBeatData(analyzedNote).IfNotNull(it => it.PerfectBeats++);
            sentenceScore.GetBeatData(analyzedNote).IfNotNull(it => it.PerfectBeats++);
        }
        else if (IsGoodHit(beatAnalyzedEvent))
        {
            ScoreData.GetBeatData(analyzedNote).IfNotNull(it => it.GoodBeats++);
            sentenceScore.GetBeatData(analyzedNote).IfNotNull(it => it.GoodBeats++);
        }
    }

    private bool IsPerfectHit(BeatAnalyzedEvent beatAnalyzedEvent)
    {
        return MidiUtils.GetRelativePitch(beatAnalyzedEvent.NoteAtBeat.MidiNote) == MidiUtils.GetRelativePitch(beatAnalyzedEvent.RecordedMidiNote);
    }

    private bool IsGoodHit(BeatAnalyzedEvent beatAnalyzedEvent)
    {
        return beatAnalyzedEvent.NoteAtBeat.MidiNote == beatAnalyzedEvent.RoundedRecordedMidiNote;
    }

    private void OnNoteAnalyzed(NoteAnalyzedEvent noteAnalyzedEvent)
    {
        if (noteAnalyzedEvent.Note.EndBeat < NextBeatToScore)
        {
            return;
        }

        Note analyzedNote = noteAnalyzedEvent.Note;
        if (ScoreData.NoteToNoteScoreMap.TryGetValue(analyzedNote, out NoteScore noteScore))
        {
            //Debug.Log($"OnNoteAnalyzed: {noteScore.correctlySungBeats} / {analyzedNote.Length}, {analyzedNote.StartBeat}, {analyzedNote.EndBeat}, {analyzedNote.Text}");
            if (noteScore.CorrectlySungBeats >= analyzedNote.Length)
            {
                noteScoreEventStream.OnNext(new NoteScoreEvent(noteScore));
            }
        }
    }

    private void OnSentenceAnalyzed(SentenceAnalyzedEvent sentenceAnalyzedEvent)
    {
        if (sentenceAnalyzedEvent.Sentence.MaxBeat < NextBeatToScore)
        {
            return;
        }

        Sentence analyzedSentence = sentenceAnalyzedEvent.Sentence;
        int totalScorableNoteLength = analyzedSentence.Notes
                .Where(note => note.IsNormal || note.IsGolden)
                .Select(note => note.Length)
                .Sum();

        if (totalScorableNoteLength <= 0)
        {
            return;
        }

        SentenceRating sentenceRating;
        if (ScoreData.SentenceToSentenceScoreMap.TryGetValue(analyzedSentence, out SentenceScore sentenceScore))
        {
            int correctlySungNoteLength = sentenceScore.NormalBeatData.PerfectAndGoodBeats + sentenceScore.GoldenBeatData.PerfectAndGoodBeats;
            double correctNotesPercentage = (double)correctlySungNoteLength / totalScorableNoteLength;

            // Score for a perfect sentence
            if (correctNotesPercentage >= SentenceRating.perfect.PercentageThreshold)
            {
                ScoreData.PerfectSentenceCount++;
            }

            sentenceRating = SentenceRating.GetSentenceRating(correctNotesPercentage);
        }
        else
        {
            sentenceScore = CreateSentenceScore(analyzedSentence);
            sentenceRating = SentenceRating.GetSentenceRating(0);
        }
        sentenceScore.TotalScoreSoFar = TotalScore;

        // Update the total score in the SceneData
        ScoreData.TotalScore = TotalScore;
        ScoreData.NormalNotesTotalScore = NormalNotesTotalScore;
        ScoreData.GoldenNotesTotalScore = GoldenNotesTotalScore;
        ScoreData.PerfectSentenceBonusTotalScore = PerfectSentenceBonusTotalScore;

        sentenceScoreEventStream.OnNext(new SentenceScoreEvent(sentenceScore, sentenceRating));
    }

    private void UpdateMaxScores(IReadOnlyCollection<Sentence> sentences)
    {
        if (sentences.IsNullOrEmpty())
        {
            // Everything is zero
            ScoreData = new PlayerScoreControlData();
            return;
        }

        // Calculate the points for a single beat of a normal or golden note
        ScoreData.NormalNoteLengthTotal = 0;
        ScoreData.GoldenNoteLengthTotal = 0;
        foreach (Sentence sentence in sentences)
        {
            ScoreData.NormalNoteLengthTotal += GetNormalNoteLength(sentence);
            ScoreData.GoldenNoteLengthTotal += GetGoldenNoteLength(sentence);
        }

        double scoreForCorrectBeatOfNormalNotes = maxScoreForNotes / ((double)ScoreData.NormalNoteLengthTotal + (2 * ScoreData.GoldenNoteLengthTotal));
        double scoreForCorrectBeatOfGoldenNotes = 2 * scoreForCorrectBeatOfNormalNotes;

        maxScoreForNormalNotes = scoreForCorrectBeatOfNormalNotes * ScoreData.NormalNoteLengthTotal;
        maxScoreForGoldenNotes = scoreForCorrectBeatOfGoldenNotes * ScoreData.GoldenNoteLengthTotal;

        // Countercheck: The sum of all points must be equal to MaxScoreForNotes
        double pointsForAllNotes = maxScoreForNormalNotes + maxScoreForGoldenNotes;
        bool isSound = Math.Abs(maxScoreForNotes - pointsForAllNotes) <= 0.01;
        if (!isSound)
        {
            Debug.LogWarning("The definition of scores for normal or golden notes is not sound: "
                + $"maxScoreForNormalNotes: {maxScoreForNormalNotes}, maxScoreForGoldenNotes: {maxScoreForGoldenNotes}, sum: {maxScoreForNormalNotes + maxScoreForGoldenNotes}");
        }

        // Round the values for the max score of normal / golden notes to avoid floating point inaccuracy.
        maxScoreForNormalNotes = Math.Ceiling(maxScoreForNormalNotes);
        maxScoreForGoldenNotes = Math.Ceiling(maxScoreForGoldenNotes);
        // The sum of the rounded points must not exceed the MaxScoreForNotes.
        // If the definition is sound then the overhang is at most 2 because of the above rounding.
        int overhang = (int)(maxScoreForNormalNotes + maxScoreForGoldenNotes) - maxScoreForNotes;
        maxScoreForNormalNotes -= overhang;

        // Remember the sentence count to calculate the points for a perfect sentence.
        ScoreData.TotalSentenceCount = sentences.Count;

        PrepareBeatToBeAnalyzedChecks();
    }
    
    private void PrepareBeatToBeAnalyzedChecks()
    {
        firstBeatToScoreInclusive = int.MaxValue;
        lastBeatToScoreExclusive = int.MinValue;
        scoredBeats.Clear();
        normalNoteBeats.Clear();
        goldenNoteBeats.Clear();
        voice.Sentences.SelectMany(s => s.Notes).ForEach(n =>
        {
            // Remember first and last beat to be analyzed
            if (n.StartBeat < firstBeatToScoreInclusive)
            {
                firstBeatToScoreInclusive = n.StartBeat;
            }
            if (n.EndBeat > lastBeatToScoreExclusive)
            {
                lastBeatToScoreExclusive = n.EndBeat;
            }
            
            // Remember the beats of the normal and golden notes to be analyzed
            HashSet<int> hashSet = null;
            if (n.IsNormal)
            {
                hashSet = normalNoteBeats;
            }
            if (n.IsGolden)
            {
                hashSet = goldenNoteBeats;
            }
            
            if (hashSet != null)
            {
                for (int beatIndex = n.StartBeat; beatIndex < n.StartBeat + n.Length; beatIndex++)
                {
                    hashSet.Add(beatIndex);
                }
            }
        });
    }

    private int GetNormalNoteLength(Sentence sentence)
    {
        return sentence.Notes.Where(note => note.IsNormal).Select(note => (int)note.Length).Sum();
    }

    private int GetGoldenNoteLength(Sentence sentence)
    {
        return sentence.Notes.Where(note => note.IsGolden).Select(note => (int)note.Length).Sum();
    }

    private SentenceScore CreateSentenceScore(Sentence sentence)
    {
        SentenceScore sentenceScore = new(sentence);
        sentenceScore.TotalScoreSoFar = TotalScore;
        return sentenceScore;
    }

    public class SentenceScoreEvent
    {
        public SentenceScore SentenceScore { get; private set; }
        public SentenceRating SentenceRating { get; private set; }

        public SentenceScoreEvent(SentenceScore sentenceScore, SentenceRating sentenceRating)
        {
            SentenceScore = sentenceScore;
            SentenceRating = sentenceRating;
        }
    }

    public class NoteScoreEvent
    {
        public NoteScore NoteScore { get; private set; }

        public NoteScoreEvent(NoteScore noteScore)
        {
            NoteScore = noteScore;
        }
    }
}
