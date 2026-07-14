namespace ColorMergeExit.Core
{
    public enum SessionState
    {
        Ready,   // built, not started (e.g. showing the "memorize the sequence" preview)
        Playing, // timer running
        Won,     // all target vehicles exited before time ran out
        Lost     // time expired
    }

    /// <summary>
    /// Wraps a <see cref="Board"/> with the time-attack rules from the design:
    /// fixed time limit, fail on timeout, retry the same map, star rating by
    /// remaining time. Engine-free: the Unity layer calls <see cref="Tick"/> each
    /// frame with Time.deltaTime and reads state for rendering.
    /// </summary>
    public sealed class GameSession
    {
        private readonly LevelData _level;

        public Board Board { get; }
        public SessionState State { get; private set; }
        public float TimeRemaining { get; private set; }

        public GameSession(LevelData level)
        {
            _level = level;
            Board = level.BuildBoard();
            TimeRemaining = level.timeLimitSeconds;
            State = SessionState.Ready;
        }

        public void Start()
        {
            if (State == SessionState.Ready) State = SessionState.Playing;
        }

        /// <summary>Advance the timer. Returns the current state after ticking.</summary>
        public SessionState Tick(float deltaSeconds)
        {
            if (State != SessionState.Playing) return State;

            // real-time block clocks: a detonating timer block loses the run immediately
            if (Board.TickRealtime(deltaSeconds)) { State = SessionState.Lost; return State; }

            TimeRemaining -= deltaSeconds;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                State = SessionState.Lost;
            }
            return State;
        }

        /// <summary>
        /// Attempt a move and re-evaluate win state. Winning stops the clock.
        /// A move after the session has ended is Invalid.
        /// </summary>
        public MoveResult Move(int blockId, int stepX, int stepY)
        {
            if (State != SessionState.Playing) return MoveResult.Invalid;

            var result = Board.TryMove(blockId, stepX, stepY);
            if (result != MoveResult.Blocked && result != MoveResult.Invalid && Board.IsWon())
                State = SessionState.Won;

            return result;
        }

        /// <summary>Undo the last move (allowed while playing). Cannot revive a lost/won run.</summary>
        public bool Undo()
        {
            if (State != SessionState.Playing) return false;
            return Board.Undo();
        }

        /// <summary>End the run as a loss right now (e.g. the board is a proven dead end), so the
        /// clock stops instead of ticking down under a failure dialog.</summary>
        public void Abort()
        {
            if (State == SessionState.Playing) State = SessionState.Lost;
        }

        /// <summary>Add time to the clock (the "+time" item). Only while playing.</summary>
        public void AddTime(float seconds)
        {
            if (State == SessionState.Playing) TimeRemaining += seconds;
        }

        /// <summary>Stars earned: 0 if not won, otherwise 1..3 based on time left.</summary>
        public int Stars()
        {
            if (State != SessionState.Won) return 0;
            if (TimeRemaining >= _level.star3SecondsLeft) return 3;
            if (TimeRemaining >= _level.star2SecondsLeft) return 2;
            return 1;
        }
    }
}
