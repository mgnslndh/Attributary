using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed record CacheKey(ResolutionSourceStrategy Strategy, string Discriminator);
