using System.Reflection;

namespace Multiplexed.AI.Runtime.AI.Rag.Mapping
{
    /// <summary>
    /// Maps public readable properties into structured RAG rows.
    ///
    /// PURPOSE:
    /// - Avoids duplicating simple row mappers across plugins.
    /// - Produces deterministic row output ordered by property name.
    /// </summary>
    public static class PropertyBasedRagRowMapper
    {
        public static IReadOnlyDictionary<string, object?> Map<TSource>(TSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var properties = typeof(TSource)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead)
                .OrderBy(x => x.Name, StringComparer.Ordinal);

            var row = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var property in properties)
            {
                row[property.Name] = property.GetValue(source);
            }

            return row;
        }

        public static IReadOnlyList<IReadOnlyDictionary<string, object?>> MapMany<TSource>(
            IEnumerable<TSource> source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return source.Select(Map).ToList();
        }
    }
}