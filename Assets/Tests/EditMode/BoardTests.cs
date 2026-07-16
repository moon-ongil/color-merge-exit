using System.Collections.Generic;
using ColorMergeExit.Core;
using NUnit.Framework;

namespace ColorMergeExit.Tests
{
    /// <summary>Tests for the block-jam board: slide colored polyominoes through matching
    /// doors (must fit), color-mixing paint cells, and clear-all win.</summary>
    public class BoardTests
    {
        private const CarColor R = CarColor.Red, B = CarColor.Blue, Y = CarColor.Yellow,
            P = CarColor.Purple, G = CarColor.Green;

        private static Exit Door(Edge e, int laneStart, int length, CarColor c) => new Exit(e, laneStart, length, c);

        [Test]
        public void Block_SlidesToMatchingDoor_ExitsAndWins()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void WrongColorDoor_IsBlocked()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, B, 5, 2) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.AreEqual(MoveResult.Blocked, board.TryMove(1, 1, 0));
            Assert.IsFalse(board.IsWon());
        }

        [Test]
        public void DoorMustFitTheBlock_NarrowBlocks_WideExits()
        {
            // A 1x2 (tall) red block at the right edge occupies rows 1 and 2; it needs a
            // door covering BOTH rows to fit through.
            var narrow = new Board(6, 6,
                new[] { Block.Rect(1, R, 5, 1, 1, 2) },
                new[] { Door(Edge.Right, 1, 1, R) });          // covers row 1 only
            Assert.AreEqual(MoveResult.Blocked, narrow.TryMove(1, 1, 0), "too narrow to fit");
            Assert.IsFalse(narrow.IsWon());

            var wide = new Board(6, 6,
                new[] { Block.Rect(1, R, 5, 1, 1, 2) },
                new[] { Door(Edge.Right, 1, 2, R) });          // covers rows 1 and 2
            Assert.AreEqual(MoveResult.Exited, wide.TryMove(1, 1, 0));
        }

        [Test]
        public void ClearingEveryBlock_Wins()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 4, 1), Block.Rect(2, B, 4, 3) },
                new[] { Door(Edge.Right, 1, 1, R), Door(Edge.Right, 3, 1, B) });
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsFalse(board.IsWon(), "one block remains");
            Assert.AreEqual(MoveResult.Exited, board.TryMove(2, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void LShape_ExitsOnlyWhenNotchPathIsClearAndDoorsCover()
        {
            // L: cells (0,0),(0,1),(1,1). Exits right through rows 0-1 if (5,0) is clear.
            var shape = new[] { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1) };
            var doors = new[] { Door(Edge.Right, 0, 2, R) };

            var blocked = new Board(6, 6,
                new Block[] { new Block(1, shape, R, 4, 0), Block.Rect(2, B, 5, 0) }, // blocker in the notch path
                doors);
            Assert.AreEqual(MoveResult.Blocked, blocked.TryMove(1, 5, 0));

            var clear = new Board(6, 6, new Block[] { new Block(1, shape, R, 4, 0) }, doors);
            Assert.AreEqual(MoveResult.Exited, clear.TryMove(1, 5, 0));
        }

        [Test]
        public void PaintCell_MixesColor_ThenExitsThroughMixedDoor()
        {
            // Red block slides right over a Blue paint cell -> becomes Purple -> exits Purple door.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, P) },
                null,
                new[] { new KeyValuePair<GridPos, CarColor>(new GridPos(3, 2), B) });
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void PaintCell_WrongMix_DoesNotOpenDoor()
        {
            // Red over Blue makes Purple, but the door is Green -> slides to edge, stays on board.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, G) },
                null,
                new[] { new KeyValuePair<GridPos, CarColor>(new GridPos(3, 2), B) });
            Assert.AreNotEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsFalse(board.IsWon());
        }

        [Test]
        public void AxisLockedBlock_MovesOnlyAlongItsRail()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(2, G, 2, 2, 2, 1, MoveAxis.Horizontal) },
                new[] { Door(Edge.Right, 0, 1, R) });
            Assert.AreEqual(MoveResult.Invalid, board.TryMove(2, 0, 1), "cannot move off its axis");
            Assert.AreEqual(0, board.MaxSlide(2, 0, 1, out _, out _, out _), "no off-axis slide room");
            Assert.AreEqual(MoveResult.Moved, board.TryMove(2, 1, 0), "moves along its axis");
        }

        [Test]
        public void SequenceDoor_CyclesColorAsBlocksPass()
        {
            // one door cycles Red -> Blue; a red block then a blue block exit through it.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 4, 2), Block.Rect(2, B, 2, 2) },
                new[] { new Exit(Edge.Right, 2, 1, new[] { R, B }) });
            Assert.AreEqual(R, board.Exits[0].CurrentColor, "door starts Red");

            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0), "red exits");
            Assert.AreEqual(B, board.Exits[0].CurrentColor, "door advanced to Blue");
            Assert.IsFalse(board.IsWon());

            Assert.AreEqual(MoveResult.Exited, board.TryMove(2, 5, 0), "blue now exits");
            Assert.IsTrue(board.IsWon());

            // undo brings the blue block back and the door back to Blue
            board.Undo();
            Assert.AreEqual(B, board.Exits[0].CurrentColor, "door index restored on undo");
        }

        [Test]
        public void Blocks_MergeIntoMixedColorOnCollision()
        {
            // slide a Red block into a Blue block -> they fuse into one Purple block.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 1, 2), Block.Rect(2, B, 3, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.IsTrue(board.MaxSlide(1, 1, 0, out _, out bool canMerge, out _) >= 1 && canMerge, "drag can reach the merge");
            Assert.AreEqual(MoveResult.Merged, board.TryMove(1, 3, 0));
            Assert.IsFalse(board.TryGetBlock(2, out _), "blue block consumed");
            Assert.IsTrue(board.TryGetBlock(1, out var b));
            Assert.AreEqual(P, b.Color, "red+blue = purple");

            // the merged purple block can now exit the purple door
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void DifferentShapes_DoNotMerge()
        {
            // red 1x1 slides into a blue 2x1 -> different shapes -> just blocks, no merge.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 1, 2), Block.Rect(2, B, 3, 2, 2, 1) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.AreEqual(MoveResult.Moved, board.TryMove(1, 3, 0), "slides up to it, no merge");
            Assert.IsTrue(board.TryGetBlock(2, out _), "blue stays");
            board.TryGetBlock(1, out var b);
            Assert.AreEqual(R, b.Color, "red unchanged (shapes differ)");
        }

        [Test]
        public void LockedBlock_Immovable_ButCanBeMergedInto()
        {
            // A locked Red block can't move, but pushing a Blue block INTO it merges -> Purple.
            var locked = new Block(3, new[] { new GridPos(0, 0) }, R, 4, 2, MoveAxis.Free, locked: true);
            var board = new Board(6, 6,
                new[] { locked, Block.Rect(2, B, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });

            Assert.AreEqual(MoveResult.Invalid, board.TryMove(3, -1, 0), "locked block can't move");
            Assert.AreEqual(MoveResult.Merged, board.TryMove(2, 3, 0), "blue merges INTO the locked red");
            Assert.IsFalse(board.TryGetBlock(3, out _), "locked block consumed by the merge");
            board.TryGetBlock(2, out var b);
            Assert.AreEqual(P, b.Color, "result is purple and free to move");
            Assert.AreEqual(MoveResult.Exited, board.TryMove(2, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void Solver_DetectsSolvableAndDeadEnd()
        {
            var ok = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2), Block.Rect(2, B, 4, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.IsTrue(Solver.IsSolvable(ok), "red+blue -> purple can reach the door");

            // a lone Red with only a Green door: can't merge, can't exit -> proven dead end
            var stuck = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, G) });
            Assert.IsFalse(Solver.IsSolvable(stuck), "no way to consume the red block");

            // shaped: two 2x1 bars merge -> purple bar exits a width-1 right door
            var shaped = new Board(6, 6,
                new[] { Block.Rect(1, R, 0, 2, 2, 1), Block.Rect(2, B, 3, 2, 2, 1) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.IsTrue(Solver.IsSolvable(shaped), "red bar slides into blue bar -> purple bar exits");
        }

        // Full re-verification that the byte-key solver still judges every shipped level solvable.
        // [Explicit] so the normal suite stays fast + path-independent; run on demand:
        //   dotnet test --filter Solver_AllShippedLevels_RemainSolvable
        // Locates StreamingAssets via the LEVELS_DIR env var, else a repo-relative fallback.
        // dotnet-only: System.Text.Json isn't in Unity, so this compiles only in the CoreTests project.
#if !UNITY_5_3_OR_NEWER
        [Test, Explicit]
        public void Solver_AllShippedLevels_RemainSolvable()
        {
            string dir = System.Environment.GetEnvironmentVariable("LEVELS_DIR");
            if (string.IsNullOrEmpty(dir))
                dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    TestContext.CurrentContext.TestDirectory, "../../../../Assets/StreamingAssets/Levels"));
            if (!System.IO.Directory.Exists(dir))
                Assert.Ignore($"levels dir not found: {dir}");

            var opts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            var files = System.IO.Directory.GetFiles(dir, "level_*.json");
            System.Array.Sort(files);
            var unsolvable = new List<string>();
            long worstMs = 0; string worst = "";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var f in files)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<LevelData>(
                    System.IO.File.ReadAllText(f), opts);
                var board = data.BuildBoard();
                var t = System.Diagnostics.Stopwatch.StartNew();
                bool ok = Solver.IsSolvable(board, 400000);
                t.Stop();
                if (t.ElapsedMilliseconds > worstMs) { worstMs = t.ElapsedMilliseconds; worst = System.IO.Path.GetFileName(f); }
                if (!ok) unsolvable.Add(System.IO.Path.GetFileName(f));
            }
            sw.Stop();
            TestContext.Out.WriteLine($"[VERIFY] levels={files.Length} unsolvable={unsolvable.Count} " +
                $"totalMs={sw.ElapsedMilliseconds} worst={worst}@{worstMs}ms");
            if (unsolvable.Count > 0)
                TestContext.Out.WriteLine("[VERIFY] UNSOLVABLE: " + string.Join(", ", unsolvable));
            Assert.IsEmpty(unsolvable, "every shipped level must remain solvable under the byte-key solver");
        }
#endif

        // Regression guard for the dead-end detection latency: a position with several free blocks
        // that can never merge (all one colour) or exit (no matching door) forces the solver to
        // exhaust a large state space to PROVE the dead end. This mirrors the in-game case that used
        // to take tens of seconds; the byte-key solver must exhaust it in well under a second.
        [Test]
        public void Solver_ProvesLargeDeadEnd_Fast()
        {
            // 3 blue blocks + a lone red door: blue can't mix with blue and can't exit a red door,
            // so it's unsolvable, but the blocks slide freely -> a large reachable state space.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, B, 0, 0), Block.Rect(2, B, 2, 2), Block.Rect(3, B, 4, 4) },
                new[] { Door(Edge.Right, 2, 1, R) });

            var snap = Solver.Capture(board);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool solvable = Solver.IsSolvable(snap, 200000, out bool capHit, out int nodes);
            sw.Stop();

            TestContext.Out.WriteLine($"[BENCH] deadend nodes={nodes} capHit={capHit} ms={sw.ElapsedMilliseconds}");
            Assert.IsFalse(solvable, "3 blue blocks with only a red door is a proven dead end");
            Assert.IsFalse(capHit, "must be a PROVEN dead end, not a give-up at the cap");
            // The sound ProvablyStranded pre-check catches this instantly (blue can never become red),
            // so it resolves in ~0 nodes; even the DFS fallback stays well under the budget.
            Assert.Less(sw.ElapsedMilliseconds, 3000, "dead-end proof must be fast");
        }

        // Repro for "stuck but no NO WAY OUT": single-colour doors close after one use. If the player
        // spends the Yellow + Purple doors on the wrong blocks, the leftover Yellow/Purple blocks can't
        // exit the only doors left (Green needs B+Y, Orange needs R+Y). The solver MUST see the used-up
        // doors as closed and report this as a dead end.
        [Test]
        public void Solver_UsedUpDoors_DeadEndDetected()
        {
            var O = CarColor.Orange;
            var blocks = new[]
            {
                Block.Rect(1, Y, 4, 1),   // leftover yellow
                Block.Rect(2, Y, 4, 3),   // leftover yellow
                Block.Rect(3, P, 4, 5),   // leftover purple
            };
            Exit Done(Edge e, int lane, CarColor c) { var d = new Exit(e, lane, 1, c); d.SetIndex(1); return d; }
            var doors = new[]
            {
                Door(Edge.Right, 1, 1, G),          // green — open, but needs B+Y
                Door(Edge.Right, 3, 1, O),          // orange — open, but needs R+Y
                Done(Edge.Right, 5, Y),             // yellow door already spent
                Done(Edge.Right, 0, P),             // purple door already spent
            };
            var board = new Board(6, 6, blocks, doors);
            Assert.IsFalse(Solver.IsSolvable(board),
                "2 yellow + 1 purple with only green/orange doors left is a proven dead end");
        }

        [Test]
        public void SameColorBlocks_DoNotMerge_JustBlock()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 1, 2), Block.Rect(2, R, 3, 2) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.AreEqual(MoveResult.Moved, board.TryMove(1, 3, 0), "slides up to it, no merge");
            Assert.IsTrue(board.TryGetBlock(2, out _), "same-color block stays");
        }

        [Test]
        public void Wall_BlocksMovement()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 0, 2) },
                new[] { Door(Edge.Right, 2, 1, R) },
                new[] { new GridPos(3, 2) });
            Assert.AreEqual(2, board.MaxSlide(1, 1, 0, out bool ex, out _, out _), "stops at the wall");
            Assert.IsFalse(ex);
        }

        [Test]
        public void Undo_RestoresPositionAndColor()
        {
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, P) },
                null,
                new[] { new KeyValuePair<GridPos, CarColor>(new GridPos(3, 2), B) });
            board.TryMove(1, 1, 0); // step onto paint -> Purple at (3,2)
            board.TryGetBlock(1, out var b);
            Assert.AreEqual(P, b.Color);
            Assert.AreEqual(3, b.X);
            Assert.IsTrue(board.Undo());
            board.TryGetBlock(1, out b);
            Assert.AreEqual(R, b.Color, "color restored");
            Assert.AreEqual(2, b.X, "position restored");
        }

        [Test]
        public void Splitter_BreaksMixedBlockIntoTwoComponents()
        {
            // A purple 1x1 slides right onto a splitter at (3,2) -> splits into red (stays) and
            // a blue twin in the cell it just vacated (2,2).
            var board = new Board(6, 6,
                new[] { Block.Rect(1, P, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R) },
                null, null,
                new[] { new GridPos(3, 2) });
            Assert.IsTrue(board.MaxSlide(1, 1, 0, out _, out _, out bool canSplit) >= 1 && canSplit, "drag reaches the splitter");
            Assert.AreEqual(MoveResult.Split, board.TryMove(1, 3, 0));
            Assert.AreEqual(2, board.BlockCount, "one block became two");
            board.TryGetBlock(1, out var mover);
            Assert.AreEqual(R, mover.Color, "mover took red");
            Assert.AreEqual(3, mover.X, "mover sits on the splitter");
            // the twin is the other block, blue, in the vacated cell (2,2)
            Block twin = null;
            foreach (var bl in board.Blocks) if (bl.Id != 1) twin = bl;
            Assert.IsNotNull(twin);
            Assert.AreEqual(B, twin.Color, "twin is blue");
            Assert.AreEqual(2, twin.X); Assert.AreEqual(2, twin.Y);
        }

        [Test]
        public void Splitter_IgnoresPrimaryBlocks_AndUndoRestores()
        {
            // a red (primary) block can't be split -> it just slides over the splitter.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2) },
                new[] { Door(Edge.Right, 5, 1, R) },
                null, null,
                new[] { new GridPos(3, 2) });
            Assert.AreEqual(MoveResult.Moved, board.TryMove(1, 1, 0), "primary passes the splitter unchanged");
            Assert.AreEqual(1, board.BlockCount);

            // undo of a split restores the single mixed block
            var board2 = new Board(6, 6,
                new[] { Block.Rect(1, P, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R) },
                null, null,
                new[] { new GridPos(3, 2) });
            Assert.AreEqual(MoveResult.Split, board2.TryMove(1, 3, 0));
            Assert.AreEqual(2, board2.BlockCount);
            Assert.IsTrue(board2.Undo());
            Assert.AreEqual(1, board2.BlockCount, "twin removed on undo");
            board2.TryGetBlock(1, out var b);
            Assert.AreEqual(P, b.Color, "color restored to purple");
            Assert.AreEqual(2, b.X, "position restored");
        }

        [Test]
        public void Splitter_SolverModelsSplitToClear()
        {
            // A purple block, a red door and a blue door: unsolvable by merge alone, but the
            // splitter turns purple -> red + blue so both doors can be fed.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, P, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R), Door(Edge.Left, 2, 1, B) },
                null, null,
                new[] { new GridPos(3, 2) });
            Assert.IsTrue(Solver.IsSolvable(board), "split lets both single-color doors be satisfied");

            // remove the splitter -> the lone purple block can't feed a red or blue door -> stuck
            var stuck = new Board(6, 6,
                new[] { Block.Rect(1, P, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R), Door(Edge.Left, 2, 1, B) });
            Assert.IsFalse(Solver.IsSolvable(stuck), "no splitter -> purple can't satisfy R/B doors");
        }

        [Test]
        public void Solver_DetectsDeadEnd_OnStuckLateGameState()
        {
            // Reproduces a reported stuck Stage-28 (7x7) state: remaining blocks are two Orange
            // (secondary), one Blue, one LOCKED Blue — the only live doors are Pink and Lime.
            // Nothing can exit (no O/B door) and nothing can merge (O+O / B+B same color,
            // O+B undefined) -> a proven dead end the detector MUST catch.
            var O = CarColor.Orange; var Bl = CarColor.Blue;
            var blocks = new[]
            {
                Block.Rect(1, O, 5, 0),
                new Block(2, new[] { new GridPos(0, 0) }, Bl, 6, 0, MoveAxis.Free, locked: true),
                Block.Rect(3, Bl, 4, 2),
                Block.Rect(4, O, 3, 3, 2, 1),
            };
            var doors = new[]
            {
                new Exit(Edge.Top, 0, 1, CarColor.Pink),
                new Exit(Edge.Bottom, 4, 1, CarColor.Lime),
            };
            var walls = new[] { new GridPos(0, 6), new GridPos(3, 1), new GridPos(3, 6), new GridPos(6, 1), new GridPos(6, 2) };
            var board = new Board(7, 7, blocks, doors, walls);
            Assert.IsFalse(Solver.IsSolvable(board), "no block can exit Pink/Lime or merge -> dead end");
        }

        [Test]
        public void ForceSplit_BreaksMixedBlockInPlace()
        {
            // the force-split item splits a Purple block into Red (in place) + a Blue twin nearby.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, P, 2, 2) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.IsTrue(board.ForceSplit(1));
            Assert.AreEqual(2, board.BlockCount, "one block became two");
            board.TryGetBlock(1, out var mover);
            Assert.AreEqual(R, mover.Color, "kept one component");
            Block twin = null;
            foreach (var bl in board.Blocks) if (bl.Id != 1) twin = bl;
            Assert.AreEqual(B, twin.Color, "twin is the other component");

            // a primary can't be force-split
            var board2 = new Board(6, 6, new[] { Block.Rect(1, R, 2, 2) }, new[] { Door(Edge.Right, 2, 1, R) });
            Assert.IsFalse(board2.ForceSplit(1));
        }

        [Test]
        public void Solver_Hint_ReturnsAFirstMoveTowardSolution()
        {
            // red + blue -> purple door: the hint must name a real block and a direction.
            var board = new Board(6, 6,
                new[] { Block.Rect(1, R, 2, 2), Block.Rect(2, B, 4, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });
            var hint = Solver.Hint(Solver.Capture(board));
            Assert.IsTrue(hint.Found, "a hint exists for a solvable board");
            Assert.IsTrue(hint.Id == 1 || hint.Id == 2, "hint names one of the blocks");
            Assert.IsTrue(hint.Dx != 0 || hint.Dy != 0, "hint has a direction");

            // performing the hinted move must be a legal, non-blocked move
            var res = board.TryMove(hint.Id, hint.Dx * 5, hint.Dy * 5);
            Assert.AreNotEqual(MoveResult.Blocked, res);
            Assert.AreNotEqual(MoveResult.Invalid, res);
        }

        [Test]
        public void Session_TimeoutLoses_AndStarsByTimeLeft()
        {
            var level = new LevelData
            {
                width = 6, height = 6, timeLimitSeconds = 30f, star2SecondsLeft = 10f, star3SecondsLeft = 20f,
                blocks = new List<BlockSpawnData>
                {
                    new BlockSpawnData { id = 1, color = R, x = 5, y = 2, w = 1, h = 1 },
                },
                doors = new List<DoorData>
                {
                    new DoorData { edge = Edge.Right, laneStart = 2, length = 1, color = R },
                },
            };

            var fast = new GameSession(level);
            fast.Start();
            fast.Tick(1f);
            Assert.AreEqual(MoveResult.Exited, fast.Move(1, 1, 0));
            Assert.AreEqual(SessionState.Won, fast.State);
            Assert.AreEqual(3, fast.Stars());

            var slow = new GameSession(level);
            slow.Start();
            Assert.AreEqual(SessionState.Lost, slow.Tick(31f));
            Assert.AreEqual(0, slow.Stars());
        }

        // ---- real-time TIMER blocks ----
        private static Block Cell(int id, CarColor c, int x, int y, float timerSeconds = 0f,
            System.Collections.Generic.IReadOnlyList<CarColor> cycle = null, float cycleSeconds = 5f) =>
            new Block(id, new[] { new GridPos(0, 0) }, c, x, y, MoveAxis.Free, false, timerSeconds, cycle, cycleSeconds);

        [Test]
        public void TimerBlock_Detonates_WhenRealTimeRunsOut()
        {
            var board = new Board(6, 6,
                new[] { Cell(1, R, 0, 2, timerSeconds: 1f) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.IsFalse(board.TickRealtime(0.5f), "still time left");
            Assert.IsFalse(board.Detonated);
            Assert.IsTrue(board.TickRealtime(0.6f), "countdown hit zero -> detonates");
            Assert.IsTrue(board.Detonated);
        }

        [Test]
        public void TimerBlock_ClearedBeforeTimeout_DoesNotDetonate()
        {
            var board = new Board(6, 6,
                new[] { Cell(1, R, 4, 2, timerSeconds: 5f) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0)); // cleared it
            Assert.IsFalse(board.TickRealtime(10f), "no timer block left to detonate");
            Assert.IsFalse(board.Detonated);
            Assert.IsTrue(board.IsWon());
        }

        [Test]
        public void GameSession_TimerDetonation_LosesRun()
        {
            var level = new LevelData
            {
                width = 6, height = 6, timeLimitSeconds = 60f,
                blocks = new System.Collections.Generic.List<BlockSpawnData>
                {
                    new BlockSpawnData { id = 1, color = R, x = 0, y = 2, timerSeconds = 1f,
                        cells = new System.Collections.Generic.List<CellData> { new CellData(0, 0) } },
                },
                doors = new System.Collections.Generic.List<DoorData>
                {
                    new DoorData { edge = Edge.Right, laneStart = 2, length = 1, color = R },
                },
            };
            var s = new GameSession(level);
            s.Start();
            Assert.AreEqual(SessionState.Playing, s.Tick(0.5f));
            Assert.AreEqual(SessionState.Lost, s.Tick(0.6f)); // timer block hits zero
        }

        // ---- real-time colour-cycling ("chameleon") blocks ----
        [Test]
        public void ChameleonBlock_CyclesColourOverRealTime()
        {
            var block = Cell(1, R, 1, 2, cycle: new[] { R, B, Y }, cycleSeconds: 2f);
            Assert.AreEqual(R, block.Color, "starts on the first colour of its cycle");
            var board = new Board(6, 6, new[] { block }, new[] { Door(Edge.Right, 5, 1, G) });
            board.TickRealtime(2f); board.TryGetBlock(1, out var b1); Assert.AreEqual(B, b1.Color);
            board.TickRealtime(2f); board.TryGetBlock(1, out var b2); Assert.AreEqual(Y, b2.Color);
            board.TickRealtime(2f); board.TryGetBlock(1, out var b3); Assert.AreEqual(R, b3.Color); // wraps
        }

        [Test]
        public void ChameleonBlock_MergesOnCurrentColour()
        {
            // chameleon starts Blue; sliding into a fixed Red block mixes Blue+Red = Purple.
            var board = new Board(6, 6,
                new[] { Cell(1, R, 2, 2, cycle: new[] { B, R }), Cell(2, R, 4, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.AreEqual(MoveResult.Merged, board.TryMove(1, 2, 0));
            board.TryGetBlock(1, out var merged);
            Assert.AreEqual(P, merged.Color);
            Assert.IsFalse(merged.IsChameleon, "a merged chameleon becomes a stable mixed block");
            Assert.AreEqual(MoveResult.Exited, board.TryMove(1, 5, 0));
            Assert.IsTrue(board.IsWon());
        }

        // ---- solver models the chameleon as a colour WILDCARD (the player can wait for any colour) ----
        [Test]
        public void Solver_Chameleon_SolvableWhenSomeCycleColourMakesTheDoor()
        {
            var board = new Board(6, 6,
                new[] { Cell(1, R, 2, 2, cycle: new[] { B, R }), Cell(2, R, 4, 2) },
                new[] { Door(Edge.Right, 2, 1, P) });
            Assert.IsTrue(Solver.IsSolvable(board));
        }

        [Test]
        public void Solver_Chameleon_UnsolvableWhenNoCycleColourMakesTheDoor()
        {
            var board = new Board(6, 6,
                new[] { Cell(1, R, 2, 2, cycle: new[] { B, R }), Cell(2, R, 4, 2) },
                new[] { Door(Edge.Right, 2, 1, G) }); // B+R only makes Purple; Green door rejects it
            Assert.IsFalse(Solver.IsSolvable(board));
        }

        [Test]
        public void Solver_IgnoresTimer_SolvableIfLogicallyClearable()
        {
            // a real-time timer never changes which moves are legal, so the solver treats it as a
            // normal block: this level is solvable (the timer's fairness is a separate calibration).
            var board = new Board(6, 6,
                new[] { Cell(1, R, 0, 2, timerSeconds: 3f) },
                new[] { Door(Edge.Right, 2, 1, R) });
            Assert.IsTrue(Solver.IsSolvable(board));
        }
    }
}
