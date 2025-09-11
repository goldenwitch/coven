// SPDX-License-Identifier: BUSL-1.1

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Coven.Spellcasting;

public static class SchemaGen
{
    public static string GetFriendlyName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        string name = type.Name.Substring(0, type.Name.IndexOf('`'));
        string args = string.Join(", ", type.GetGenericArguments()
                                            .Select(GetFriendlyName));
        return $"{name}<{args}>";
    }

    /// <summary>
    /// Generates a JSON Schema (as text) for the given CLR <paramref name="type"/>.
    /// Uses .NET 9's System.Text.Json schema exporter.
    /// </summary>
    /// <param name="type">The CLR type to describe.</param>
    /// <param name="configureSerializer">Optional hook to tweak JsonSerializerOptions (naming, numbers, etc.).</param>
    /// <param name="configureExporter">Optional hook to tweak JsonSchemaExporterOptions (e.g., nullability treatment).</param>
    /// <param name="writeIndented">Pretty‑print the output JSON.</param>
    public static string GenerateSchema(
        Type type,
        Action<JsonSerializerOptions>? configureSerializer = null,
        Action<JsonSchemaExporterOptions>? configureExporter = null,
        bool writeIndented = true)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerOptions.Default);
        configureSerializer?.Invoke(serializerOptions);

        var exporterOptions = new JsonSchemaExporterOptions();
        configureExporter?.Invoke(exporterOptions);

        JsonNode schemaNode = serializerOptions.GetJsonSchemaAsNode(type, exporterOptions);

        // Optional: add a dialect marker if you want one visible in the file
        if (schemaNode is JsonObject obj && !obj.ContainsKey("$schema"))
        {
            obj.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        }

        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = writeIndented });
    }
}