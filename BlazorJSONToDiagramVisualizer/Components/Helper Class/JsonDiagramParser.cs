using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Syncfusion.Blazor.Diagram;

public static class JsonDiagramParser
{
    private const double DEFAULT_NODE_WIDTH = 150;
    private const double DEFAULT_NODE_HEIGHT = 50;

    public static DiagramData ProcessData(string jsonData)
    {
        var diagramData = new DiagramData();

        using var doc = JsonDocument.Parse(jsonData);
        var data = doc.RootElement;
        if (data.ValueKind != JsonValueKind.Object || !data.EnumerateObject().Any())
            return diagramData;

        // Determine rootNodeId
        string rootNodeId = "root";
        var props = data.EnumerateObject().ToList();
        if (props.Count == 1 && props[0].Value.ValueKind == JsonValueKind.Object)
            rootNodeId = props[0].Name;

        // Separate primitive and non-primitive properties
        var primitiveProps = new List<JsonProperty>();
        var nonPrimitiveProps = new List<JsonProperty>();
        foreach (var prop in props)
        {
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                nonPrimitiveProps.Add(prop);
            else
                primitiveProps.Add(prop);
        }

        bool rootCreated = false;
        // 1) Merge primitive props into the root node
        if (primitiveProps.Any())
        {
            rootCreated = true;
            var ann = new DiagramObjectCollection<ShapeAnnotation>();
            var lines = new List<string>();
            foreach (var p in primitiveProps)
            {
                var key = p.Name;
                var raw = p.Value.ToString();
                var val = FormatValue(raw);
                ann.Add(new ShapeAnnotation { ID = $"Key_{key}", Content = $"{key}:" });
                ann.Add(new ShapeAnnotation { ID = $"Value_{key}", Content = val });
                lines.Add($"{key}: {val}");
            }
            var merged = string.Join("\n", lines);
            var rootIdPascal = ConvertUnderScoreToPascalCase(rootNodeId);
            var rootNode = new Node
            {
                ID = rootIdPascal,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = ann,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = true,
                    ["mergedContent"] = merged
                },
                Data = new { path = "Root", title = merged, actualdata = merged }
            };
            diagramData.Nodes.Add(rootNode);
            rootNodeId = rootIdPascal;
        }

        // 2) Process non-primitive properties
        foreach (var prop in nonPrimitiveProps)
        {
            var key = prop.Name;
            var element = prop.Value;
            var nodeId = ConvertUnderScoreToPascalCase(key);
            var childCount = GetObjectLength(element);

            var ann = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = key }
            };
            if (childCount > 0)
                ann.Add(new ShapeAnnotation { Content = $"{{{childCount}}}" });

            var merged = $"{key}  {{{childCount}}}";
            var node = new Node
            {
                ID = nodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = ann,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = false,
                    ["mergedContent"] = merged
                },
                Data = new { path = $"Root.{key}", title = key, actualdata = key }
            };
            diagramData.Nodes.Add(node);

            if (rootCreated)
            {
                diagramData.Connectors.Add(new Connector
                {
                    ID = $"connector-{rootNodeId}-{nodeId}",
                    SourceID = rootNodeId,
                    TargetID = nodeId
                });
            }

            ProcessNestedData(element, nodeId, diagramData.Nodes, diagramData.Connectors, $"Root.{key}", key);
        }

        CheckMultiRoot(diagramData.Nodes, diagramData.Connectors);
        return diagramData;
    }

    private static void ProcessNestedData(
        JsonElement element,
        string parentId,
        List<Node> nodeList,
        List<Connector> connectorList,
        string parentPath,
        string keyName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    index++;
                    continue;
                }
                var nodeId = ConvertUnderScoreToPascalCase($"{parentId}-{index}");

                if (item.ValueKind != JsonValueKind.Object)
                {
                    // Primitive in array
                    var raw = item.ToString();
                    var val = FormatValue(raw);
                    var ann = new DiagramObjectCollection<ShapeAnnotation>
                    {
                        new ShapeAnnotation { ID = $"Value_{keyName}", Content = val }
                    };
                    nodeList.Add(new Node
                    {
                        ID = nodeId,
                        Width = DEFAULT_NODE_WIDTH,
                        Height = DEFAULT_NODE_HEIGHT,
                        Annotations = ann,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["isLeaf"] = true,
                            ["mergedContent"] = val
                        },
                        Data = new { path = $"{parentPath}[{index}]", title = val, actualdata = val }
                    });
                    connectorList.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });
                }
                else
                {
                    // Object in array
                    var obj = item;
                    // Merge primitive children
                    var prims = obj.EnumerateObject()
                        .Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array)
                        .ToList();
                    if (prims.Any())
                    {
                        var ann = new DiagramObjectCollection<ShapeAnnotation>();
                        var lines = new List<string>();
                        foreach (var p in prims)
                        {
                            var k = p.Name;
                            var raw = p.Value.ToString();
                            var val = FormatValue(raw);
                            ann.Add(new ShapeAnnotation { ID = $"Key_{k}", Content = $"{k}:" });
                            ann.Add(new ShapeAnnotation { ID = $"Value_{k}", Content = val });
                            lines.Add($"{k}: {val}");
                        }
                        var merged = string.Join("\n", lines);
                        nodeList.Add(new Node
                        {
                            ID = nodeId,
                            Width = DEFAULT_NODE_WIDTH,
                            Height = DEFAULT_NODE_HEIGHT,
                            Annotations = ann,
                            AdditionalInfo = new Dictionary<string, object>
                            {
                                ["isLeaf"] = true,
                                ["mergedContent"] = merged
                            },
                            Data = new { path = $"{parentPath}[{index}]", title = merged, actualdata = merged }
                        });
                        connectorList.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });
                    }
                    // Recurse into nested
                    foreach (var child in obj.EnumerateObject()
                        .Where(p => p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array))
                    {
                        var childId = ConvertUnderScoreToPascalCase($"{nodeId}-{child.Name}");
                        var childPath = $"{parentPath}[{index}].{child.Name}";
                        var count = GetObjectLength(child.Value);
                        var annChild = new DiagramObjectCollection<ShapeAnnotation>
                        {
                            new ShapeAnnotation { Content = child.Name }
                        };
                        if (count > 0)
                            annChild.Add(new ShapeAnnotation { Content = $"{{{count}}}" });
                        var mergedChild = $"{child.Name}  {{{count}}}";
                        nodeList.Add(new Node
                        {
                            ID = childId,
                            Width = DEFAULT_NODE_WIDTH,
                            Height = DEFAULT_NODE_HEIGHT,
                            Annotations = annChild,
                            AdditionalInfo = new Dictionary<string, object>
                            {
                                ["isLeaf"] = false,
                                ["mergedContent"] = mergedChild
                            },
                            Data = new { path = childPath, title = child.Name, actualdata = child.Name }
                        });
                        connectorList.Add(new Connector { ID = $"connector-{nodeId}-{childId}", SourceID = nodeId, TargetID = childId });
                        ProcessNestedData(child.Value, childId, nodeList, connectorList, childPath, child.Name);
                    }
                }
                index++;
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var props2 = element.EnumerateObject().ToList();
            var prims2 = props2.Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array).ToList();
            var nonPrims2 = props2.Where(p => p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array).ToList();

            if (prims2.Any())
            {
                var ann = new DiagramObjectCollection<ShapeAnnotation>();
                var lines = new List<string>();
                foreach (var p in prims2)
                {
                    var k = p.Name;
                    var raw = p.Value.ToString();
                    var val = FormatValue(raw);
                    ann.Add(new ShapeAnnotation { ID = $"Key_{k}", Content = $"{k}:" });
                    ann.Add(new ShapeAnnotation { ID = $"Value_{k}", Content = val });
                    lines.Add($"{k}: {val}");
                }
                var merged = string.Join("\n", lines);
                var leafId = ConvertUnderScoreToPascalCase($"{parentId}-leaf");
                nodeList.Add(new Node
                {
                    ID = leafId,
                    Width = DEFAULT_NODE_WIDTH,
                    Height = DEFAULT_NODE_HEIGHT,
                    Annotations = ann,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["isLeaf"] = true,
                        ["mergedContent"] = merged
                    },
                    Data = new { path = $"{parentPath}.leaf", title = merged, actualdata = merged }
                });
                connectorList.Add(new Connector { ID = $"connector-{parentId}-{leafId}", SourceID = parentId, TargetID = leafId });
            }

            foreach (var prop in nonPrims2)
            {
                var key = prop.Name;
                var childId = ConvertUnderScoreToPascalCase($"{parentId}-{key}");
                var count = GetObjectLength(prop.Value);
                var ann = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { Content = key } };
                if (count > 0)
                    ann.Add(new ShapeAnnotation { Content = $"{{{count}}}" });
                var merged = $"{key}  {{{count}}}";
                nodeList.Add(new Node
                {
                    ID = childId,
                    Width = DEFAULT_NODE_WIDTH,
                    Height = DEFAULT_NODE_HEIGHT,
                    Annotations = ann,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["isLeaf"] = false,
                        ["mergedContent"] = merged
                    },
                    Data = new { path = $"{parentPath}.{key}", title = key, actualdata = key }
                });
                connectorList.Add(new Connector { ID = $"connector-{parentId}-{childId}", SourceID = parentId, TargetID = childId });
                ProcessNestedData(prop.Value, childId, nodeList, connectorList, $"{parentPath}.{key}", key);
            }
        }
    }

    private static void CheckMultiRoot(List<Node> nodeList, List<Connector> connectorList)
    {
        var allIds = nodeList.Select(n => n.ID).ToList();
        var incoming = new HashSet<string>(connectorList.Select(c => c.TargetID));
        var roots = allIds.Where(id => !incoming.Contains(id)).ToList();
        if (roots.Count > 1)
        {
            const string mainRootId = "main-root";
            nodeList.Add(new Node
            {
                ID = mainRootId,
                Width = 40,
                Height = 40,
                Annotations = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { Content = "" } },
                Data = new { path = "MainRoot", title = "", actualdata = "" }
            });
            foreach (var r in roots)
                connectorList.Add(new Connector { ID = $"connector-{mainRootId}-{r}", SourceID = mainRootId, TargetID = r });
        }
    }

    private static int GetObjectLength(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();
        if (element.ValueKind != JsonValueKind.Object)
            return 0;
        var props = element.EnumerateObject().ToList();
        int prim = props.Count(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array);
        int arr = props.Count(p => p.Value.ValueKind == JsonValueKind.Array);
        int obj = props.Count(p => p.Value.ValueKind == JsonValueKind.Object);
        return (prim > 0 ? 1 : 0) + arr + obj;
    }

    private static string ConvertUnderScoreToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        var parts = input.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(char.ToUpper(part[0]));
            sb.Append(part.Substring(1));
        }
        return sb.ToString();
    }

    private static string FormatValue(string v)
    {
        if (bool.TryParse(v, out var b))
            return b.ToString().ToLower();
        if (double.TryParse(v, out var d))
            return d.ToString();
        return $"\"{v}\"";
    }
}

public class DiagramData
{
    public List<Node> Nodes { get; set; } = new List<Node>();
    public List<Connector> Connectors { get; set; } = new List<Connector>();
}

public enum EditorInputType
{
    json,
    xml
}
