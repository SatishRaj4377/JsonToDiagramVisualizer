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
    private static int annotationIdCounter = 0;

    public static DiagramData ProcessData(string jsonData)
    {
        var diagramData = new DiagramData();
        annotationIdCounter = 0;
        using var doc = JsonDocument.Parse(jsonData);
        var data = doc.RootElement;

        // Handle different root types
        if (data.ValueKind == JsonValueKind.Array)
        {
            // Handle direct array at root
            ProcessDirectArray(data, diagramData);
            CheckMultiRoot(diagramData.Nodes, diagramData.Connectors);
            return diagramData;
        }

        if (data.ValueKind != JsonValueKind.Object || !data.EnumerateObject().Any())
            return diagramData;

        // Determine rootNodeId and handle preprocessing
        string rootNodeId = "root";
        JsonElement processedData = data;
        bool shouldSkipEmptyRoot = false;

        var props = data.EnumerateObject().ToList();
        if (props.Count == 1)
        {
            var singleProp = props[0];
            if (string.IsNullOrWhiteSpace(singleProp.Name) && singleProp.Value.ValueKind == JsonValueKind.Object)
            {
                shouldSkipEmptyRoot = true;
                processedData = singleProp.Value;
            }
            else if (!string.IsNullOrWhiteSpace(singleProp.Name) && singleProp.Value.ValueKind == JsonValueKind.Object)
            {
                rootNodeId = singleProp.Name;
            }
        }

        // Process the data (either original or preprocessed)
        var processedProps = processedData.EnumerateObject().ToList();

        // Separate primitive and non-primitive properties
        var primitiveProps = new List<JsonProperty>();
        var nonPrimitiveProps = new List<JsonProperty>();
        foreach (var prop in processedProps)
        {
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                nonPrimitiveProps.Add(prop);
            else
                primitiveProps.Add(prop);
        }

        bool rootCreated = false;
        string finalRootId = shouldSkipEmptyRoot ? "data-root" : ConvertUnderScoreToPascalCase(rootNodeId);

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
                ann.Add(new ShapeAnnotation { ID = $"Key_{++annotationIdCounter}", Content = $"{key}:" });
                ann.Add(new ShapeAnnotation { ID = $"Value_{++annotationIdCounter}", Content = val });
                lines.Add($"{key}: {val}");
            }
            var merged = string.Join("\n", lines);
            var rootNode = new Node
            {
                ID = finalRootId,
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
            rootNodeId = finalRootId;
        }

        // 2) Process non-primitive properties
        foreach (var prop in nonPrimitiveProps)
        {
            if (IsEmpty(prop.Value))
                continue;

            var key = prop.Name;
            var element = prop.Value;
            var nodeId = ConvertUnderScoreToPascalCase(key);
            var childCount = GetObjectLength(element);

            var ann = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID=$"Parent-{++annotationIdCounter}",Content = key }
            };
            if (childCount > 0)
                ann.Add(new ShapeAnnotation { ID = $"Count-{++annotationIdCounter}", Content = $"{{{childCount}}}" });

            var merged = $"{key}   {{{childCount}}}";
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

        // Handle multiple root nodes scenario
        if ((shouldSkipEmptyRoot || HasMultipleRoots(diagramData.Nodes, diagramData.Connectors)) && !rootCreated)
        {
            CheckMultiRoot(diagramData.Nodes, diagramData.Connectors);
        }

        return diagramData;
    }

    private static void ProcessDirectArray(JsonElement arrayElement, DiagramData diagramData)
    {
        if (arrayElement.GetArrayLength() == 0)
            return;

        // Create a root node for the array
        string rootNodeId = "array-root";
        var rootNode = new Node
        {
            ID = rootNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID=$"{++annotationIdCounter}", Content = "Array" },
                new ShapeAnnotation { ID=$"{++annotationIdCounter}",Content = $"{{{arrayElement.GetArrayLength()}}}" }
            },
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = $"Array {{{arrayElement.GetArrayLength()}}}"
            },
            Data = new { path = "Root", title = "Array", actualdata = "Array" }
        };
        diagramData.Nodes.Add(rootNode);

        // Process array elements
        ProcessNestedData(arrayElement, rootNodeId, diagramData.Nodes, diagramData.Connectors, "Root", "Array");
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
            if (IsEmpty(element))
                return;

            int index = 0;

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                {
                    index++;
                    continue;
                }
                var nodeId = ConvertUnderScoreToPascalCase($"{parentId}-{index}");

                if (item.ValueKind != JsonValueKind.Object && item.ValueKind != JsonValueKind.Array)
                {
                    // Primitive in array
                    var raw = item.ToString();
                    var val = FormatValue(raw);
                    var ann = new DiagramObjectCollection<ShapeAnnotation>
                    {
                        new ShapeAnnotation {ID=$"{++annotationIdCounter}", Content = val }
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
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    // Nested array
                    var childCount = GetObjectLength(item);
                    var ann = new DiagramObjectCollection<ShapeAnnotation>
                    {
                        new ShapeAnnotation { ID=$"{++annotationIdCounter}",Content = $"Item {index}" }
                    };
                    if (childCount > 0)
                        ann.Add(new ShapeAnnotation { ID = $"{++annotationIdCounter}", Content = $"{{{childCount}}}" });

                    var merged = $"Item {index} {{{childCount}}}";
                    nodeList.Add(new Node
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
                        Data = new { path = $"{parentPath}[{index}]", title = $"Item {index}", actualdata = $"Item {index}" }
                    });
                    connectorList.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });
                    ProcessNestedData(item, nodeId, nodeList, connectorList, $"{parentPath}[{index}]", $"Item {index}");
                }
                else
                {
                    // Object in array
                    var obj = item;
                    var objProps = obj.EnumerateObject().ToList();

                    // Separate primitive and non-primitive properties
                    var prims = objProps.Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array).ToList();
                    var nonPrims = objProps.Where(p => (p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array) && !IsEmpty(p.Value)).ToList();

                    bool requiresIntermediateNode = prims.Any() || nonPrims.Count > 1;

                    if (requiresIntermediateNode)
                    {
                        // Create intermediate node for the object
                        if (prims.Any())
                        {
                            var ann = new DiagramObjectCollection<ShapeAnnotation>();
                            var lines = new List<string>();
                            foreach (var p in prims)
                            {
                                var k = p.Name;
                                var raw = p.Value.ToString();
                                var val = FormatValue(raw);
                                ann.Add(new ShapeAnnotation { ID = $"Key_{++annotationIdCounter}", Content = $"{k}:" });
                                ann.Add(new ShapeAnnotation { ID = $"Value_{++annotationIdCounter}", Content = val });
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
                        }
                        else
                        {
                            var content = $"Item   {index}";
                            var ann = new DiagramObjectCollection<ShapeAnnotation>
                            {
                                new ShapeAnnotation { ID=$"{++annotationIdCounter}",Content = content }
                            };
                            nodeList.Add(new Node
                            {
                                ID = nodeId,
                                Width = DEFAULT_NODE_WIDTH,
                                Height = DEFAULT_NODE_HEIGHT,
                                Annotations = ann,
                                AdditionalInfo = new Dictionary<string, object>
                                {
                                    ["isLeaf"] = false,
                                    ["mergedContent"] = content
                                },
                                Data = new { path = $"{parentPath}[{index}]", title = content, actualdata = content }
                            });
                        }

                        connectorList.Add(new Connector { ID = $"connector-{parentId}-{nodeId}", SourceID = parentId, TargetID = nodeId });

                        // Process nested properties
                        foreach (var child in nonPrims)
                        {
                            var childId = ConvertUnderScoreToPascalCase($"{nodeId}-{child.Name}");
                            var childPath = $"{parentPath}[{index}].{child.Name}";
                            var count = GetObjectLength(child.Value);
                            var annChild = new DiagramObjectCollection<ShapeAnnotation>
                            {
                                new ShapeAnnotation { ID=$"Parent- {++annotationIdCounter}",Content = child.Name }
                            };
                            if (count > 0)
                                annChild.Add(new ShapeAnnotation { ID = $"Count-{++annotationIdCounter}", Content = $"{{{count}}}" });
                            var mergedChild = $"{child.Name}   {{{count}}}";
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
                    else if (nonPrims.Count == 1)
                    {
                        // Direct connection for single nested object
                        var singleChild = nonPrims[0];
                        var childId = ConvertUnderScoreToPascalCase($"{nodeId}-{singleChild.Name}");
                        var childPath = $"{parentPath}[{index}].{singleChild.Name}";
                        var count = GetObjectLength(singleChild.Value);
                        var annChild = new DiagramObjectCollection<ShapeAnnotation>
                        {
                            new ShapeAnnotation { ID=$"Parent- {++annotationIdCounter}",Content = singleChild.Name }
                        };
                        if (count > 0)
                            annChild.Add(new ShapeAnnotation { ID = $"Count-{++annotationIdCounter}", Content = $"{{{count}}}" });
                        var mergedChild = $"{singleChild.Name}   {{{count}}}";
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
                            Data = new { path = childPath, title = singleChild.Name, actualdata = singleChild.Name }
                        });
                        connectorList.Add(new Connector { ID = $"connector-{parentId}-{childId}", SourceID = parentId, TargetID = childId });
                        ProcessNestedData(singleChild.Value, childId, nodeList, connectorList, childPath, singleChild.Name);
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
            var nonPrims2 = props2.Where(p => (p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array) && !IsEmpty(p.Value)).ToList();

            if (prims2.Any())
            {
                var ann = new DiagramObjectCollection<ShapeAnnotation>();
                var lines = new List<string>();
                foreach (var p in prims2)
                {
                    var k = p.Name;
                    var raw = p.Value.ToString();
                    var val = FormatValue(raw);
                    ann.Add(new ShapeAnnotation { ID = $"Key_{++annotationIdCounter}", Content = $"{k}:" });
                    ann.Add(new ShapeAnnotation { ID = $"Value_{++annotationIdCounter}", Content = val });
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
                var ann = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { ID = $"Parent-{++annotationIdCounter}", Content = key } };
                if (count > 0)
                    ann.Add(new ShapeAnnotation { ID = $"Count-{++annotationIdCounter}", Content = $"{{{count}}}" });
                var merged = $"{key}   {{{count}}}";
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

    private static bool HasMultipleRoots(List<Node> nodeList, List<Connector> connectorList)
    {
        var allIds = nodeList.Select(n => n.ID).ToList();
        var incoming = new HashSet<string>(connectorList.Select(c => c.TargetID));
        var roots = allIds.Where(id => !incoming.Contains(id)).ToList();
        return roots.Count > 1;
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
                Annotations = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { ID = $"{++annotationIdCounter}", Content = "" } },
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = false
                },
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
            if (part.Length > 0)
            {
                sb.Append(char.ToUpper(part[0]));
                sb.Append(part.Substring(1));
            }
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

    private static bool IsEmpty(JsonElement element)
    {
        return (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0) ||
               (element.ValueKind == JsonValueKind.Object && !element.EnumerateObject().Any());
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