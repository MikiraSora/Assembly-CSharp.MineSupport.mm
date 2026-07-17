using System;
using System.Collections.Generic;
using MAI2.Util;
using Manager;
using Monitor;
using UnityEngine;

namespace MineSupport
{
    internal static class MineRuntime
    {
        private static readonly Dictionary<GameScoreList, HashSet<int>> MineScoreIndices =
            new Dictionary<GameScoreList, HashSet<int>>();

        private static readonly Dictionary<object, bool> RuntimeMineState =
            new Dictionary<object, bool>();

        private static int feedbackSuppressionDepth;

        internal static bool FeedbackSuppressed => feedbackSuppressionDepth > 0;

        internal static bool HasMineMarker(MA2Record record)
        {
            if (record == null)
                return false;

            for (uint i = 0; i < 32; i++)
            {
                var field = record.getStr(i);
                if (string.IsNullOrEmpty(field))
                    continue;

                if (IsMineTailField(field))
                    return true;
            }

            return false;
        }

        private static bool IsMineTailField(string field)
        {
            var compact = field.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            return compact == "!m"
                || compact.StartsWith("!m#", StringComparison.Ordinal)
                || compact.EndsWith("!m", StringComparison.Ordinal)
                || compact.IndexOf("#!m", StringComparison.Ordinal) >= 0;
        }

        internal static bool IsMine(NoteData note)
        {
            return note != null && ((patch_NoteData)(object)note).isMine;
        }

        internal static bool IsMine(object runtimeObject)
        {
            bool isMine;
            return runtimeObject != null
                && RuntimeMineState.TryGetValue(runtimeObject, out isMine)
                && isMine;
        }

        internal static void BindRuntime(object runtimeObject, NoteData note, int monitorId, int noteIndex)
        {
            var isMine = IsMine(note);
            if (runtimeObject != null)
                RuntimeMineState[runtimeObject] = isMine;

            if (!isMine)
                return;

            PatchLog.WriteLine($"[Mine] bind monitor={monitorId}, note={noteIndex}");

            try
            {
                var score = Singleton<GamePlayManager>.Instance.GetGameScore(monitorId);
                if (score == null)
                    return;

                HashSet<int> indices;
                if (!MineScoreIndices.TryGetValue(score, out indices))
                {
                    indices = new HashSet<int>();
                    MineScoreIndices.Add(score, indices);
                }

                indices.Add(noteIndex);
            }
            catch
            {
                // A score object may not exist during preload; the normal result path remains valid.
            }
        }

        internal static void ResetScore(GameScoreList score)
        {
            if (score != null)
                MineScoreIndices.Remove(score);
        }

        internal static bool IsMineScore(GameScoreList score, int noteIndex)
        {
            HashSet<int> indices;
            return score != null
                && MineScoreIndices.TryGetValue(score, out indices)
                && indices.Contains(noteIndex);
        }

        internal static NoteJudge.ETiming ConvertMineResult(NoteJudge.ETiming result)
        {
            var mapped = result == NoteJudge.ETiming.TooFast || result == NoteJudge.ETiming.TooLate
                ? NoteJudge.ETiming.Critical
                : NoteJudge.ETiming.TooLate;
            PatchLog.WriteLine($"[Mine] result {result} -> {mapped}");
            return mapped;
        }

        internal static void RunNoteCheck(object runtimeObject, Action original)
        {
            if (!IsMine(runtimeObject))
            {
                original();
                return;
            }

            var previousAutoPlay = GameManager.AutoPlay;
            GameManager.AutoPlay = GameManager.AutoPlayMode.None;
            feedbackSuppressionDepth++;
            try
            {
                original();
            }
            finally
            {
                feedbackSuppressionDepth--;
                GameManager.AutoPlay = previousAutoPlay;
            }
        }

        internal static void RunFeedbackSuppressed(object runtimeObject, Action original)
        {
            if (!IsMine(runtimeObject))
            {
                original();
                return;
            }

            feedbackSuppressionDepth++;
            try
            {
                original();
            }
            finally
            {
                feedbackSuppressionDepth--;
            }
        }

        internal static void ApplyVisual(MonoBehaviour note, bool mine)
        {
            MineVisual.Apply(note, mine);
        }

        internal static void ClearVisual(MonoBehaviour note)
        {
            MineVisual.Clear(note);
            if (note != null)
                RuntimeMineState.Remove(note);
        }

        internal static void ReleaseRuntime(MonoBehaviour note)
        {
            if (note == null)
                return;

            if (IsMine(note))
                MineVisual.Clear(note);

            RuntimeMineState.Remove(note);
        }

        internal static void NormalizeEach(NoteDataList notes)
        {
            if (notes == null)
                return;

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                if (IsMine(note))
                {
                    note.isEach = false;
                    note.indexEach = -1;
                    note.parent = null;
                    note.eachChild.Clear();
                }
            }

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                note.eachChild.RemoveAll(IsMine);
                if (note.eachChild.Count == 0)
                {
                    note.isEach = false;
                    note.indexEach = -1;
                }
            }
        }
    }
}
