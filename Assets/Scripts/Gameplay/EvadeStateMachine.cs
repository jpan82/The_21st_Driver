using UnityEngine;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Converts noisy side intents into stable evade behavior with confirmation,
    /// minimum commit time, and smooth return-to-lane transitions.
    /// </summary>
    public sealed class EvadeStateMachine
    {
        public enum State
        {
            KeepLane,
            EvadeLeft,
            EvadeRight,
            ReturnToLane
        }

        public struct Config
        {
            // Consecutive frames required before a side intent is considered valid.
            public int sideConfirmFrames;
            // Minimum time to keep an evade side before switching/returning.
            public float evadeCommitSeconds;
            // Lateral offset magnitude considered "back on lane center".
            public float returnDoneThreshold;
        }

        private State state = State.KeepLane;
        private float stateMinUntil;
        private float evadeTargetLateral;
        private int pendingSide;
        private int pendingSideFrames;

        public State CurrentState => state;

        /// <summary>
        /// Clears runtime state when a car is initialized/reset.
        /// </summary>
        public void Reset()
        {
            state = State.KeepLane;
            stateMinUntil = 0f;
            evadeTargetLateral = 0f;
            pendingSide = 0;
            pendingSideFrames = 0;
        }

        public float Step(
            bool inStrategyRange,
            int desiredSide,
            int action,
            float currentLateralOffset,
            float timeNow,
            Config config,
            System.Func<int, float> actionToLateralTarget)
        {
            // 1) Filter side intents through short confirmation.
            UpdatePendingSide(desiredSide);
            bool confirmedSide = pendingSide != 0 && pendingSideFrames >= Mathf.Max(1, config.sideConfirmFrames);

            // 2) Outside strategy range, only allow return-to-lane behavior.
            if (!inStrategyRange)
            {
                if (state == State.EvadeLeft || state == State.EvadeRight)
                {
                    state = State.ReturnToLane;
                }
                else if (Mathf.Abs(currentLateralOffset) <= config.returnDoneThreshold)
                {
                    state = State.KeepLane;
                }
                return 0f;
            }

            switch (state)
            {
                case State.KeepLane:
                    // Start evade only after side intent is confirmed.
                    if (confirmedSide)
                    {
                        EnterEvade(action, pendingSide, timeNow, config.evadeCommitSeconds, actionToLateralTarget);
                    }
                    break;

                case State.EvadeLeft:
                case State.EvadeRight:
                    // During commit window, keep current side.
                    if (timeNow >= stateMinUntil)
                    {
                        int currentSide = state == State.EvadeLeft ? -1 : 1;
                        if (confirmedSide && pendingSide == -currentSide)
                        {
                            EnterEvade(action, pendingSide, timeNow, config.evadeCommitSeconds, actionToLateralTarget);
                        }
                        else if (!confirmedSide)
                        {
                            state = State.ReturnToLane;
                        }
                    }
                    break;

                case State.ReturnToLane:
                    // Allow re-entering evade if a fresh side intent is confirmed.
                    if (confirmedSide)
                    {
                        EnterEvade(action, pendingSide, timeNow, config.evadeCommitSeconds, actionToLateralTarget);
                    }
                    else if (Mathf.Abs(currentLateralOffset) <= config.returnDoneThreshold)
                    {
                        state = State.KeepLane;
                    }
                    break;
            }

            // Non-evade states return zero to let caller blend back to lane center.
            return (state == State.EvadeLeft || state == State.EvadeRight) ? evadeTargetLateral : 0f;
        }

        /// <summary>
        /// Tracks side intent consistency across frames.
        /// desiredSide: -1 left, +1 right, 0 none.
        /// </summary>
        private void UpdatePendingSide(int desiredSide)
        {
            if (desiredSide == 0)
            {
                pendingSide = 0;
                pendingSideFrames = 0;
                return;
            }

            if (pendingSide == desiredSide)
            {
                pendingSideFrames++;
            }
            else
            {
                pendingSide = desiredSide;
                pendingSideFrames = 1;
            }
        }

        private void EnterEvade(
            int action,
            int side,
            float timeNow,
            float commitSeconds,
            System.Func<int, float> actionToLateralTarget)
        {
            // Derive a concrete lateral target from current AI action.
            state = side < 0 ? State.EvadeLeft : State.EvadeRight;
            evadeTargetLateral = actionToLateralTarget != null ? actionToLateralTarget(action) : 0f;
            if (Mathf.Abs(evadeTargetLateral) < 0.01f)
            {
                // Fallback to unit direction so the state still has motion intent.
                evadeTargetLateral = side < 0 ? -1f : 1f;
            }

            // Commit window prevents instant left-right flapping.
            stateMinUntil = timeNow + Mathf.Max(0f, commitSeconds);
        }
    }
}
