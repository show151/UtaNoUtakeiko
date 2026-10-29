
using System;
using UnityEngine;

namespace Uta.GameFlow
{
    public class GameSessionManager : MonoBehaviour
    {
        public GameSessionState CurrentState { get; private set; }
            = GameSessionState.Title;

        public event Action<GameSessionState, GameSessionState>
            OnStateChanged;

        private bool isTransitioning;
        private bool recoveryRequested;

        private void Start()
        {
            Debug.Log($"Session initialized: {CurrentState}");
        }

        public bool TryTransition(GameSessionState nextState)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("Transition already in progress.");
                return false;
            }

            if (CurrentState == nextState)
            {
                Debug.LogWarning("Duplicate transition blocked.");
                return false;
            }

            if (!IsValidTransition(CurrentState, nextState))
            {
                Debug.LogWarning(
                    $"Invalid transition: {CurrentState} -> {nextState}"
                );
                return false;
            }

            isTransitioning = true;

            try
            {
                ChangeState(nextState);
                return !recoveryRequested;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                recoveryRequested = true;
                return false;
            }
            finally
            {
                isTransitioning = false;

                if (recoveryRequested)
                {
                    recoveryRequested = false;
                    RecoverToTitle();
                }
            }
        }

        private void ChangeState(GameSessionState nextState)
        {
            GameSessionState previousState = CurrentState;
            CurrentState = nextState;

            Debug.Log($"Session: {previousState} -> {nextState}");
            OnStateChanged?.Invoke(previousState, nextState);
        }

        public void RecoverToTitle()
        {
            if (isTransitioning)
            {
                recoveryRequested = true;
                Debug.LogWarning("Recovery queued.");
                return;
            }

            if (CurrentState == GameSessionState.Title)
                return;

            isTransitioning = true;

            try
            {
                ChangeState(GameSessionState.Title);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                isTransitioning = false;
                recoveryRequested = false;
            }
        }

        private bool IsValidTransition(
            GameSessionState current,
            GameSessionState next)
        {
            if (next == GameSessionState.Error)
                return true;

            switch (current)
            {
                case GameSessionState.Title:
                    return next == GameSessionState.DifficultySelect;

                case GameSessionState.DifficultySelect:
                    return next == GameSessionState.SongSelect
                        || next == GameSessionState.Title;

                case GameSessionState.SongSelect:
                    return next == GameSessionState.Countdown
                        || next == GameSessionState.DifficultySelect;

                case GameSessionState.Countdown:
                    return next == GameSessionState.Playing
                        || next == GameSessionState.Title;

                case GameSessionState.Playing:
                    return next == GameSessionState.Playback;

                case GameSessionState.Playback:
                    return next == GameSessionState.Result;

                case GameSessionState.Result:
                    return next == GameSessionState.Ranking;

                case GameSessionState.Ranking:
                    return next == GameSessionState.Announcement;

                case GameSessionState.Announcement:
                    return next == GameSessionState.Title;

                case GameSessionState.Error:
                    return next == GameSessionState.Title;

                default:
                    return false;
            }
        }
    }
}
