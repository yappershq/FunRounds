using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace FunRounds.Utils;

/// <summary>
/// Thin localization helper over <see cref="ILocalizerManager"/>.
/// Missing LocalizerManager degrades to a silent no-op.
/// </summary>
internal static class Loc
{
    /// <summary>Localized chat line to one client.</summary>
    public static void Chat(ILocalizerManager? lm, IGameClient client, string key, params object?[] args)
        => lm?.For(client).Localized(key, args).Prefix(null)
              .Transform(ChatFormat.ProcessColorCodes).Print(HudPrintChannel.Chat);

    /// <summary>
    /// Build a localized string for one client (no chat print) — for HUD/win-panel text.
    /// Returns empty when the localizer is unavailable so callers can skip showing nothing.
    /// </summary>
    public static string Text(ILocalizerManager? lm, IGameClient client, string key, params object?[] args)
        => lm is null ? string.Empty : lm.For(client).Localized(key, args).Prefix(null).Build();

    /// <summary>Localized chat line to every in-game human.</summary>
    public static void ChatAll(ILocalizerManager? lm, IClientManager clients, string key, params object?[] args)
    {
        if (lm is null) return;
        foreach (var client in clients.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient) continue;
            lm.For(client).Localized(key, args).Prefix(null)
              .Transform(ChatFormat.ProcessColorCodes).Print(HudPrintChannel.Chat);
        }
    }
}
