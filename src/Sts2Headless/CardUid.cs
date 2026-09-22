using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace Sts2Headless;

/// <summary>
/// Stable per-object identity for cards within one process.
///
/// <para>
/// Card IDs (CARD.STRIKE_SILENT) do not identify a card: a deck holds several Strikes, and
/// comparing a simulator replay against this engine needs to know which one moved. The UID is
/// allocated on first sight and held in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, so it
/// follows the object and does not keep it alive.
/// </para>
///
/// <para>
/// The numeric value is allocation-ordered and therefore only meaningful inside one process. It
/// identifies a card across states of the same run; it is not a persistent deck-card identifier
/// and must not be used to associate a shop/reward card with a clone in another run.
/// </para>
/// </summary>
internal static class CardUid
{
    private static readonly ConditionalWeakTable<CardModel, object> Ids = new();
    private static int _next;

    public static int Of(CardModel? card) =>
        card is null ? -1 : (int)Ids.GetValue(card, _ => (object)Interlocked.Increment(ref _next));
}
