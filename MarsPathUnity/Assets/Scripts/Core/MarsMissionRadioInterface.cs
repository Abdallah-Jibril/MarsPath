using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private MarsMissionRadio missionRadio;
        private bool showQuestRadio;
        internal bool MissionRadioReceiving => missionRadio != null && missionRadio.Receiving;
        internal bool QuestRadioSetupVisible => showQuestRadio;
        internal bool QuestRadioConnected => missionRadio != null && missionRadio.Connected;
        internal string QuestRadioStatus => missionRadio?.LinkStatus ?? "STARTING RADIO";
        internal string QuestRadioMicStatus => missionRadio?.MicrophoneStatus;
        internal bool QuestRadioTalking => missionRadio != null && missionRadio.Talking;
        internal string MissionRadioActivity => QuestRadioTalking ? "Talking" : MissionRadioReceiving ? "Receiving"
            : missionRadio != null && missionRadio.TalkRequested ? "Waiting for link" : null;
        internal Color MissionRadioAccent => MissionRadioReceiving ? new Color(.34f, .90f, 1f)
            : QuestRadioTalking || missionRadio != null && missionRadio.TalkRequested ? new Color(1f, .72f, .38f)
            : QuestRadioConnected ? new Color(.43f, .90f, .70f) : UiMuted;

        internal void ToggleQuestRadioSetup() => showQuestRadio = !showQuestRadio;

        internal void PauseQuestRadio() => missionRadio?.SuspendQuestRadio();

        private void UpdateMissionRadio(bool inMission)
        {
            bool quest = Application.platform == RuntimePlatform.Android;
            if (Application.isBatchMode || !quest && Application.platform != RuntimePlatform.WindowsPlayer) return;
            if (!inMission) showQuestRadio = false;
            if (missionRadio == null)
            {
                var audioObject = new GameObject("Mission Control radio");
                audioObject.transform.SetParent(mainCamera.transform, false);
                missionRadio = audioObject.AddComponent<MarsMissionRadio>();
                missionRadio.Initialize(quest);
            }
            bool focused = Application.isFocused;
            if (quest) AdvertiseQuestWaiting(focused);
            bool voicePage = inMission || (quest ? questMissionControl && GlobalView.IsVisible : !GlobalView.IsVisible && !helmetPreview);
            bool talkHeld = voicePage && focused && (quest ? MarsQuestControls.LeftTriggerPressed() : MissionControlTalkHeld());
            missionRadio.Tick(voicePage, focused, talkHeld);
            if (!quest) TickDesktopDispatch();
            if (quest && missionRadio.TryTakeRoute(out var id, out byte[] payload))
                StartCoroutine(LoadDispatchedPlan(id, payload));
            TickMissionSynchronization(quest);
        }

        private static bool MissionControlTalkHeld()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.Y)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.yKey.isPressed == true;
#else
            return false;
#endif
        }

        private void DrawMissionRadio()
        {
            if (missionRadio == null || helmetPreview || !Application.isFocused) return;
            var rect = new Rect(22, 814, 238, 58);
            Color accent = MissionRadioAccent;
            VisorPanel(rect, accent);
            string status = missionRadio.MicrophoneStatus ?? missionRadio.LinkStatus;
            string label = MissionRadioActivity ?? "HOLD TO TALK";
            GUI.Label(new Rect(rect.x + 15, rect.y + 5, rect.width - 26, 19), status, visorSmall);
            GUI.Label(new Rect(rect.x + 16, rect.y + 28, 29, 22), "Y", visorKeycap);
            Color previous = GUI.contentColor;
            GUI.contentColor = accent;
            GUI.Label(new Rect(rect.x + 55, rect.y + 23, rect.width - 62, 32), label, visorHeading);
            GUI.contentColor = previous;
        }
    }
}
