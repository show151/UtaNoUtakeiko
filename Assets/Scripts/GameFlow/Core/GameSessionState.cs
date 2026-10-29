namespace Uta.GameFlow
{
    /// <summary>
    /// ゲームセッション全体の進行状態。
    /// </summary>
    public enum GameSessionState
    {
        Title,              // タイトル画面
        DifficultySelect,   // 難易度選択
        SongSelect,         // 楽曲選択
        Countdown,          // プレイ開始カウントダウン
        Playing,            // 音符入力フェーズ
        Playback,           // 歌唱再生フェーズ
        Result,             // リザルト画面
        Ranking,            // ランキング画面
        Announcement,       // X-LIVE告知画面
        Error               // エラー発生・復旧処理
    }
}