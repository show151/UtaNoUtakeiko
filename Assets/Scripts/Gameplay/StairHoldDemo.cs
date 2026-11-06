using UnityEngine;
using UnityEngine.InputSystem;
using UtaNoUtakeiko.Gameplay;

// Standalone demonstration: Enter is the yellow button, Space is the red button.
public sealed class StairHoldDemo : MonoBehaviour
{
    private readonly float[] targets = { 0.75f, 0.60f, 0.45f, 0.30f };
    private readonly string[] names = { "ファ", "ミ", "レ", "ド" };
    private readonly string[] judgments = new string[4];
    private StairHoldSession session;
    private MovingBar bar;
    private bool running;
    private bool finished;
    private GUIStyle textStyle;
    private GUIStyle titleStyle;

    private void Awake()
    {
        var cameraObject = new GameObject("Demo Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.07f, 0.13f);
        var barObject = new GameObject("Demo Moving Bar");
        bar = barObject.AddComponent<MovingBar>();
        ResetDemo();
    }

    private void ResetDemo()
    {
        session = new StairHoldSession(targets.Length);
        System.Array.Clear(judgments, 0, judgments.Length);
        running = false;
        finished = false;
        bar.StopStairMovement();
        bar.ResumeMovement();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetDemo();
            return;
        }

        if (!finished && !running && keyboard.enterKey.wasPressedThisFrame)
        {
            session.Begin();
            running = true;
            bar.StartStairMovement();
            CaptureCurrentPosition();
        }

        if (running && keyboard.enterKey.isPressed && keyboard.spaceKey.wasPressedThisFrame)
        {
            CaptureCurrentPosition();
        }

        if (running && keyboard.enterKey.wasReleasedThisFrame)
            FinishDemo();
    }

    private void CaptureCurrentPosition()
    {
        int index = session.CapturedCount;
        float position = bar.NormalizedPosition;
        if (!session.TryCapture(position)) return;

        float difference = Mathf.Abs(position - targets[index]);
        judgments[index] = difference <= 0.02f ? "Perfect" :
            difference <= 0.05f ? "Great" : difference <= 0.10f ? "Good" :
            difference <= 0.20f ? "Bad" : "Miss";
        if (session.IsComplete) FinishDemo();
    }

    private void FinishDemo()
    {
        session.End();
        running = false;
        finished = true;
        bar.StopStairMovement();
        bar.ResumeMovement();
    }

    private void OnGUI()
    {
        if (textStyle == null)
        {
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
            textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, normal = { textColor = Color.white } };
            titleStyle = new GUIStyle(textStyle) { fontSize = 32, fontStyle = FontStyle.Bold };
        }
        GUI.Label(new Rect(30, 20, 850, 50), "階段長押しデモ", titleStyle);
        GUI.Label(new Rect(30, 75, 900, 38), "黄色：Enterを押し続ける　赤：Spaceで判定　R：やり直し", textStyle);
        GUI.Label(new Rect(30, 112, 1100, 38), "最初のファは黄色で判定、ミ → レ → ドは黄色を押したまま赤で判定", textStyle);
        float top = 180f;
        float height = Mathf.Max(200f, Mathf.Min(420f, Screen.height - top - 70f));
        float x = 100f;
        GUI.color = new Color(0.2f, 0.25f, 0.34f);
        GUI.DrawTexture(new Rect(x, top, 280f, height), Texture2D.whiteTexture);
        for (int i = 0; i < targets.Length; i++)
        {
            float y = top + (1f - targets[i]) * height;
            GUI.color = i == session.CapturedCount && !finished ? Color.yellow : Color.white;
            GUI.DrawTexture(new Rect(x, y, 280f, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(400f, y - 16f, 350f, 32f), $"{names[i]}　{judgments[i] ?? "—"}", textStyle);
        }
        GUI.color = new Color(1f, 0.35f, 0.25f);
        GUI.DrawTexture(new Rect(x - 10f, top + (1f - bar.NormalizedPosition) * height - 4f, 300f, 8f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string status = finished ? (session.IsComplete ? "完了！ Rで再挑戦" : "途中で離しました。Rで再挑戦") :
            running ? "長押し中：赤で判定" : "黄色を押して開始";
        GUI.Label(new Rect(30, top + height + 15f, 800f, 40f), status, textStyle);
    }
}
