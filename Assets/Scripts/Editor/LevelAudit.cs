using System;
using System.Diagnostics;
using System.Text;
using ColorMergeExit.Core;
using ColorMergeExit.Game;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Batch-tests every shipped level through the real game runtime (the same C# code the
    /// simulator runs): loads the JSON, builds the Board, checks every colour renders, and runs
    /// the C# Solver to confirm each level is actually solvable. Run via:
    ///   Unity -batchmode -executeMethod ColorMergeExit.Editor.LevelAudit.Run -quit
    /// Reports any level that fails to load, references an unhandled colour, crashes on build, or
    /// the C# solver proves UNSOLVABLE (a Python/C# mismatch bug).
    /// </summary>
    public static class LevelAudit
    {
        public static void Run()
        {
            const int total = 500;
            const int cap = 120000;
            int ok = 0, loadFail = 0, colorIssue = 0, ctorFail = 0, unsolvable = 0, solveErr = 0;
            var problems = new StringBuilder();
            var sw = Stopwatch.StartNew();

            for (int i = 1; i <= total; i++)
            {
                LevelData lvl = null;
                try { lvl = LevelRepository.Load(i); }
                catch (Exception e) { loadFail++; problems.AppendLine($"L{i}: LOAD EXCEPTION {e.Message}"); continue; }

                // Load() silently falls back to a default tutorial level on failure; a real level
                // has id == i, so a mismatch means the JSON failed to parse/read.
                if (lvl == null || lvl.id != i || lvl.blocks == null || lvl.blocks.Count == 0)
                { loadFail++; problems.AppendLine($"L{i}: JSON did not load (fell back to default)"); continue; }

                // Colour validity: nothing should fall through ToUnity to the gray "unknown" default.
                bool colBad = false;
                foreach (var b in lvl.blocks)
                    if (VisualAssets.ToUnity(b.color) == Color.gray)
                    { colBad = true; problems.AppendLine($"L{i}: block colour {b.color} unhandled (renders gray)"); }
                foreach (var d in lvl.doors)
                {
                    var seq = (d.colorSequence != null && d.colorSequence.Count > 0)
                        ? d.colorSequence.ToArray() : new[] { d.color };
                    foreach (var c in seq)
                        if (VisualAssets.ToUnity(c) == Color.gray)
                        { colBad = true; problems.AppendLine($"L{i}: door colour {c} unhandled (renders gray)"); }
                }
                if (colBad) colorIssue++;

                Board board;
                try { board = new GameSession(lvl).Board; }
                catch (Exception e) { ctorFail++; problems.AppendLine($"L{i}: BOARD BUILD EXCEPTION {e.Message}"); continue; }

                try
                {
                    // IsSolvable returns true on cap (conservative), so a FALSE is a proven dead board.
                    if (!Solver.IsSolvable(board, cap))
                    { unsolvable++; problems.AppendLine($"L{i}: C# SOLVER proves UNSOLVABLE"); }
                    else ok++;
                }
                catch (Exception e) { solveErr++; problems.AppendLine($"L{i}: SOLVER EXCEPTION {e.Message}"); }

                if (i % 50 == 0)
                    Debug.Log($"[LevelAudit] ...{i}/{total} ({sw.Elapsed.TotalSeconds:F0}s) ok={ok} problems={loadFail + colorIssue + ctorFail + unsolvable + solveErr}");
            }

            sw.Stop();
            Debug.Log($"[LevelAudit] DONE {total} levels in {sw.Elapsed.TotalSeconds:F0}s\n" +
                      $"  PASS (loaded+built+solvable): {ok}\n" +
                      $"  loadFail={loadFail}  colorIssues={colorIssue}  buildCrash={ctorFail}  UNSOLVABLE={unsolvable}  solverErr={solveErr}\n" +
                      (problems.Length == 0 ? "  NO PROBLEMS ✓" : "--- PROBLEMS ---\n" + problems));
        }
    }
}
