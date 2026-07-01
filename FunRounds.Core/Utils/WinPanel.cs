using System;
using FunRounds;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;

namespace FunRounds.Utils;

/// <summary>
/// Prominent round-reveal notification via the CS2 win-panel (<c>cs_win_panel_round</c> +
/// <c>funfact_token</c>) — the MVP/win panel hijacked to show a message mid-round. The panel has
/// NO built-in timeout, so it's hidden after a duration by firing <c>round_freeze_end</c> to each
/// client on a timer. Lifted from TTT.PlayerHUD's proven ShowTimedWinPanel + FormatWinPanelHtml.
/// </summary>
internal static class WinPanel
{
    /// <summary>
    /// Wrap text in the CS2 win-panel font (large bold mono). The trailing line breaks push the
    /// text up into the visible funfact area of the panel.
    /// </summary>
    public static string Format(string text, string color)
        => $"<font class='fontSize-xxl fontWeight-Bold stratum-bold-mono' color='{color}'>{text}</font><br><br><br><br><br><br>";

    /// <summary>
    /// Show the win-panel to every in-game player (HTML built per-client via <paramref name="htmlFor"/>
    /// so text is localized in each player's language), then clear it after durationSeconds.
    /// </summary>
    public static void ShowTimed(InterfaceBridge bridge, Func<IGameClient, string> htmlFor, int durationSeconds)
    {
        foreach (var client in bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient || !client.IsInGame) continue;
            if (client.GetPlayerController() is not { } ctrl || !ctrl.IsValid()) continue;

            var html = htmlFor(client);
            if (string.IsNullOrEmpty(html)) continue;

            var e = bridge.EventManager.CreateEvent("cs_win_panel_round", true);
            if (e is null) continue;

            e.SetBool("show_timer_defend", false);
            e.SetBool("show_timer_attack", false);
            e.SetInt("timer_time", -1);
            e.SetInt("final_event", 16); // GameCommencing — forces the panel to render
            e.SetPlayer("funfact_player", ctrl);
            e.SetString("funfact_token", html);
            e.FireToClient(client);
            e.Dispose();
        }

        // The win panel does not self-expire — clear it after the display duration.
        // StopOnRoundEnd also guarantees a reveal can't linger into the next round.
        if (durationSeconds > 0)
            bridge.ModSharp.PushTimer(() => Clear(bridge), durationSeconds,
                GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
    }

    private static void Clear(InterfaceBridge bridge)
    {
        foreach (var client in bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient || !client.IsInGame) continue;
            var e = bridge.EventManager.CreateEvent("round_freeze_end", true);
            if (e is null) continue;
            e.FireToClient(client);
            e.Dispose();
        }
    }
}
