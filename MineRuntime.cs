using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mai2.Mai2Cue;
using MAI2.Util;
using DB;
using Manager;
using Monitor;
using UnityEngine;

namespace MineSupport
{
    internal static class MineRuntime
    {
        private sealed class RuntimeBinding
        {
            internal bool IsMine;
            internal GameScoreList Score;
            internal int MonitorId;
            internal int NoteIndex;
            internal bool HasOutcome;
            internal NoteJudge.ETiming FinalTiming;
            internal NoteScore.EScoreType Kind;
            internal MineResultSource OutcomeSource;
            internal bool FeedbackEmitted;
        }

        private static readonly Dictionary<GameScoreList, HashSet<int>> MineScoreIndices =
            new Dictionary<GameScoreList, HashSet<int>>(ReferenceEqualityComparer<GameScoreList>.Instance);

        private static readonly Dictionary<object, RuntimeBinding> RuntimeBindings =
            new Dictionary<object, RuntimeBinding>(ReferenceEqualityComparer<object>.Instance);

        [ThreadStatic]
        private static int feedbackSuppressionDepth;

        [ThreadStatic]
        private static int autoPlaySuppressionDepth;

        [ThreadStatic]
        private static MineResultSource resultSource;

        [ThreadStatic]
        private static GameScoreList resultScore;

        [ThreadStatic]
        private static int resultIndex;

        [ThreadStatic]
        private static object resultRuntimeObject;

        internal static bool FeedbackSuppressed => feedbackSuppressionDepth > 0;

        internal static bool SuppressAutoPlay => autoPlaySuppressionDepth > 0;

        internal static bool IsMine(NoteData note)
        {
            return note != null && ((patch_NoteData)(object)note).isMine;
        }

        internal static bool IsMine(object runtimeObject)
        {
            RuntimeBinding binding;
            return runtimeObject != null
                && RuntimeBindings.TryGetValue(runtimeObject, out binding)
                && binding.IsMine;
        }

        internal static void RegisterScore(GameScoreList score, int monitorId)
        {
            if (score == null)
                return;

            ResetScore(score);
            var indices = new HashSet<int>();
            try
            {
                var notes = NotesManager.Instance(monitorId).getReader().GetNoteList();
                if (notes != null)
                {
                    for (var i = 0; i < notes.Count; i++)
                    {
                        if (IsMine(notes[i]))
                            indices.Add(notes[i].indexNote);
                    }
                }
            }
            catch (Exception exception)
            {
                PatchLog.ErrorOnce(
                    "mine-score-registration",
                    $"[Mine] failed to register score note map for monitor={monitorId}: {exception.GetType().Name}: {exception.Message}");
            }

            MineScoreIndices[score] = indices;
        }

        internal static void BindRuntime(object runtimeObject, NoteData note, int monitorId, int noteIndex)
        {
            if (runtimeObject == null)
                return;

            RuntimeBinding binding;
            if (!RuntimeBindings.TryGetValue(runtimeObject, out binding))
            {
                binding = new RuntimeBinding();
                RuntimeBindings.Add(runtimeObject, binding);
            }

            binding.IsMine = IsMine(note);
            binding.MonitorId = monitorId;
            binding.NoteIndex = noteIndex;
            binding.Score = null;
            binding.HasOutcome = false;
            binding.FinalTiming = NoteJudge.ETiming.End;
            binding.Kind = NoteScore.EScoreType.End;
            binding.OutcomeSource = MineResultSource.None;
            binding.FeedbackEmitted = false;

            if (!binding.IsMine)
                return;

            try
            {
                binding.Score = Singleton<GamePlayManager>.Instance.GetGameScore(monitorId);
                if (binding.Score != null)
                {
                    HashSet<int> indices;
                    if (!MineScoreIndices.TryGetValue(binding.Score, out indices))
                    {
                        indices = new HashSet<int>();
                        MineScoreIndices.Add(binding.Score, indices);
                    }

                    indices.Add(noteIndex);
                }
            }
            catch (Exception exception)
            {
                PatchLog.ErrorOnce(
                    "mine-runtime-binding",
                    $"[Mine] failed to bind monitor={monitorId}, note={noteIndex}: {exception.GetType().Name}: {exception.Message}");
            }

            PatchLog.WriteLine($"[Mine] bind monitor={monitorId}, note={noteIndex}");
        }

        internal static void ResetScore(GameScoreList score)
        {
            if (score == null)
                return;

            MineScoreIndices.Remove(score);
            var staleObjects = new List<object>();
            foreach (var pair in RuntimeBindings)
            {
                if (pair.Value.Score == score)
                    staleObjects.Add(pair.Key);
            }

            for (var i = 0; i < staleObjects.Count; i++)
            {
                MineVisual.Clear(staleObjects[i] as MonoBehaviour);
                RuntimeBindings.Remove(staleObjects[i]);
            }
        }

        internal static bool IsMineScore(GameScoreList score, int noteIndex)
        {
            HashSet<int> indices;
            return score != null
                && MineScoreIndices.TryGetValue(score, out indices)
                && indices.Contains(noteIndex);
        }

        internal static MineScope EnterNoteCheck(object runtimeObject)
        {
            var scope = CaptureScope();
            RuntimeBinding binding;
            if (runtimeObject == null
                || !RuntimeBindings.TryGetValue(runtimeObject, out binding)
                || !binding.IsMine)
            {
                return scope;
            }

            resultSource = MineResultSource.Live;
            resultScore = binding.Score;
            resultIndex = binding.NoteIndex;
            resultRuntimeObject = runtimeObject;
            feedbackSuppressionDepth++;
            autoPlaySuppressionDepth++;
            return scope;
        }

        internal static MineScope EnterFeedback(object runtimeObject)
        {
            var scope = CaptureScope();
            RuntimeBinding binding;
            if (runtimeObject == null
                || !RuntimeBindings.TryGetValue(runtimeObject, out binding)
                || !binding.IsMine)
            {
                return scope;
            }

            feedbackSuppressionDepth++;
            if (resultSource == MineResultSource.None)
            {
                resultSource = MineResultSource.Live;
                resultScore = binding.Score;
                resultIndex = binding.NoteIndex;
                resultRuntimeObject = runtimeObject;
            }

            return scope;
        }

        internal static MineScope EnterForcedTimeout(int monitorId, NoteData note)
        {
            var scope = CaptureScope();
            if (!IsMine(note))
                return scope;

            resultSource = MineResultSource.ForcedTimeout;
            resultScore = GetScore(monitorId);
            resultIndex = note.indexNote;
            resultRuntimeObject = null;
            feedbackSuppressionDepth++;
            return scope;
        }

        internal static MineScope EnterNaturalFinish(GameScoreList score)
        {
            var scope = CaptureScope();
            resultSource = MineResultSource.NaturalFinish;
            resultScore = score;
            resultIndex = -1;
            resultRuntimeObject = null;
            return scope;
        }

        internal static MineScope EnterVisibleMineFeedback()
        {
            var scope = CaptureScope();
            feedbackSuppressionDepth = 0;
            return scope;
        }

        internal static void Exit(MineScope scope)
        {
            feedbackSuppressionDepth = scope.FeedbackDepth;
            autoPlaySuppressionDepth = scope.AutoPlayDepth;
            resultSource = scope.ResultSource;
            resultScore = scope.ResultScore;
            resultIndex = scope.ResultIndex;
            resultRuntimeObject = scope.ResultRuntimeObject;
        }

        internal static bool TryConvertResult(
            GameScoreList score,
            int noteIndex,
            NoteJudge.ETiming original,
            out NoteJudge.ETiming mapped)
        {
            mapped = original;
            var targetMatches = resultSource == MineResultSource.NaturalFinish
                ? resultScore == score
                : resultScore == score && resultIndex == noteIndex;
            if (!MinePolicyCore.ShouldConvert(IsMineScore(score, noteIndex), resultSource, targetMatches))
                return false;

            mapped = (NoteJudge.ETiming)MinePolicyCore.ConvertTiming(
                (int)original,
                (int)NoteJudge.ETiming.TooFast,
                (int)NoteJudge.ETiming.TooLate,
                (int)NoteJudge.ETiming.Critical);
            PatchLog.WriteLine(
                $"[Mine] result source={resultSource}, score={RuntimeHelpers.GetHashCode(score)}, index={noteIndex}, original={original}, mapped={mapped}");
            return true;
        }

        internal static void RecordResult(
            GameScoreList score,
            int noteIndex,
            NoteScore.EScoreType kind,
            bool converted)
        {
            if (!converted || resultRuntimeObject == null)
                return;

            RuntimeBinding binding;
            if (!RuntimeBindings.TryGetValue(resultRuntimeObject, out binding)
                || !binding.IsMine
                || binding.Score != score
                || binding.NoteIndex != noteIndex)
            {
                return;
            }

            try
            {
                binding.HasOutcome = true;
                binding.FinalTiming = (NoteJudge.ETiming)score.GetScoreAt(noteIndex).Timing;
                binding.Kind = kind;
                binding.OutcomeSource = resultSource;
            }
            catch (Exception exception)
            {
                PatchLog.ErrorOnce(
                    "mine-result-recording",
                    $"[Mine] failed to record final result for note={noteIndex}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        internal static bool TryBeginLiveMiss(object runtimeObject)
        {
            RuntimeBinding binding;
            if (runtimeObject == null
                || !RuntimeBindings.TryGetValue(runtimeObject, out binding)
                || !binding.IsMine
                || !binding.HasOutcome
                || binding.OutcomeSource != MineResultSource.Live
                || binding.FinalTiming != NoteJudge.ETiming.TooLate
                || binding.FeedbackEmitted
                || binding.Score == null
                || binding.Score.IsTrackSkip)
            {
                return false;
            }

            binding.FeedbackEmitted = true;
            PlayMissSound(binding.MonitorId, binding.Kind);
            return true;
        }

        internal static void ApplyVisual(MonoBehaviour note, bool mine, GameObject suppressedOverlay = null)
        {
            MineVisual.Apply(note, mine, suppressedOverlay);
        }

        internal static void PrepareRuntime(MonoBehaviour note)
        {
            if (note == null)
                return;

            MineVisual.Clear(note);
            RuntimeBindings.Remove(note);
        }

        internal static void ReleaseRuntime(MonoBehaviour note)
        {
            if (note == null)
                return;

            MineVisual.Clear(note);
            RuntimeBindings.Remove(note);
        }

        internal static void ClearTransientState()
        {
            MineVisual.ClearAll();
            RuntimeBindings.Clear();
            MineScoreIndices.Clear();
            feedbackSuppressionDepth = 0;
            autoPlaySuppressionDepth = 0;
            resultSource = MineResultSource.None;
            resultScore = null;
            resultIndex = -1;
            resultRuntimeObject = null;
        }

        internal readonly struct MineScope
        {
            internal MineScope(
                int feedbackDepth,
                int autoPlayDepth,
                MineResultSource source,
                GameScoreList score,
                int index,
                object runtimeObject)
            {
                FeedbackDepth = feedbackDepth;
                AutoPlayDepth = autoPlayDepth;
                ResultSource = source;
                ResultScore = score;
                ResultIndex = index;
                ResultRuntimeObject = runtimeObject;
            }

            internal int FeedbackDepth { get; }
            internal int AutoPlayDepth { get; }
            internal MineResultSource ResultSource { get; }
            internal GameScoreList ResultScore { get; }
            internal int ResultIndex { get; }
            internal object ResultRuntimeObject { get; }
        }

        private static MineScope CaptureScope()
        {
            return new MineScope(
                feedbackSuppressionDepth,
                autoPlaySuppressionDepth,
                resultSource,
                resultScore,
                resultIndex,
                resultRuntimeObject);
        }

        private static GameScoreList GetScore(int monitorId)
        {
            try
            {
                return Singleton<GamePlayManager>.Instance.GetGameScore(monitorId);
            }
            catch
            {
                return null;
            }
        }

        private static void PlayMissSound(int monitorId, NoteScore.EScoreType kind)
        {
            try
            {
                var score = GetScore(monitorId);
                if (score == null)
                    return;

                var volume = score.UserOption.TapHoldVolume;
                switch (kind)
                {
                    case NoteScore.EScoreType.Break:
                        volume = score.UserOption.BreakVolume;
                        break;
                    case NoteScore.EScoreType.Slide:
                        volume = score.UserOption.SlideVolume;
                        break;
                    case NoteScore.EScoreType.Touch:
                        volume = score.UserOption.TouchVolume;
                        break;
                }

                if (volume != 0)
                    SoundManager.PlayGameSE(Cue.SE_GAME_TOUCH_HOLD_MISS, monitorId, volume.GetValue());
            }
            catch (Exception exception)
            {
                PatchLog.ErrorOnce(
                    "mine-miss-sound",
                    $"[Mine] failed to play native MISS cue for monitor={monitorId}: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
