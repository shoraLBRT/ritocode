namespace Ritocode.Modules.Attempts.Studying;

/// <summary>A card the caller has marked «Изучено», and when.</summary>
public sealed record StudyMarkView(string Card, DateTimeOffset StudiedAt);

/// <summary><c>GET /me/studied</c>: the caller's marks on published cards, oldest first.</summary>
public sealed record StudiedCardsView(IReadOnlyList<StudyMarkView> Cards);
