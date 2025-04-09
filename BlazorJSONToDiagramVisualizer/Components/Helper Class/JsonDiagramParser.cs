using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Syncfusion.Blazor.Diagram;

public static class JsonDiagramParser
{
    private const int MAX_NODE_CONTENT_LENGTH = 100;
    private const double stableIconHeight = 39.25;

    public static DiagramData ProcessData(JsonElement data)
    {
        var diagramData = new DiagramData();

        if (data.ValueKind != JsonValueKind.Object || data.EnumerateObject().Count() == 0)
            return diagramData;

        // Choose a root node id: if one property exists and is an object, use that key; otherwise "root"
        string rootNodeId = "root";
        var properties = data.EnumerateObject().ToList();
        if (properties.Count == 1 && properties[0].Value.ValueKind == JsonValueKind.Object)
        {
            rootNodeId = properties[0].Name;
        }

        // Split object properties: primitives vs non-primitives
        var nonLeafNodes = new List<string>();
        var primitives = new List<string>();

        foreach (var prop in properties)
        {
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                nonLeafNodes.Add(prop.Name);
            else
                primitives.Add(prop.Name);
        }

        bool rootCreated = false;
        // If there are primitive properties, merge them into a node.
        if (primitives.Count > 0)
        {
            rootCreated = true;
            string mergedContent = string.Join("\n", primitives.Select(key => $"{key}: {data.GetProperty(key)}"));
            double nodeWidth = CalculateWidth(mergedContent, MAX_NODE_CONTENT_LENGTH, false);
            double nodeHeight = CalculateHeight(mergedContent, MAX_NODE_CONTENT_LENGTH);
            var rootNode = new Node
            {
                ID = rootNodeId,
                Width = nodeWidth,
                Height = nodeHeight,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = mergedContent }
                },
                Data = new { path = "Root", title = mergedContent, actualdata = mergedContent }
            };
            diagramData.Nodes.Add(rootNode);
        }

        // Process each non-primitive property as a child node.
        foreach (var key in nonLeafNodes)
        {
            string nodeId = key;
            double nodeWidth = CalculateWidth(key, MAX_NODE_CONTENT_LENGTH, false);
            double nodeHeight = CalculateHeight(key, MAX_NODE_CONTENT_LENGTH);
            int childCount = GetObjectLength(data.GetProperty(key));
            string textContent = childCount > 0 ? $"{key} {{{childCount}}}" : key;

            var childNode = new Node
            {
                ID = nodeId,
                Width = nodeWidth,
                Height = nodeHeight,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = textContent }
                },
                Data = new { path = $"Root.{key}", title = key, actualdata = key, displayContent = new { key = new string[] { key }, displayValue = childCount } }
            };
            diagramData.Nodes.Add(childNode);

            // If a root node (with primitives) was created, link it to these children.
            if (rootCreated)
            {
                diagramData.Connectors.Add(new Connector
                {
                    ID = $"connector-{rootNodeId}-{nodeId}",
                    SourceID = rootNodeId,
                    TargetID = nodeId
                });
            }

            // Process nested data recursively.
            ProcessNestedData(data.GetProperty(key), nodeId, diagramData.Nodes, diagramData.Connectors, $"Root.{key}", key);
        }

        CheckMultiRoot(diagramData.Nodes, diagramData.Connectors);
        return diagramData;
    }

    // Recursive parser for nested JSON data.
    private static void ProcessNestedData(JsonElement element, string parentId, List<Node> nodeList, List<Connector> connectorList, string parentPath, string keyName)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array)
            return;

        if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                    continue;
                string nodeId = $"{parentId}-{index}";
                // For objects within an array, recursively process
                if (item.ValueKind == JsonValueKind.Object)
                {
                    ProcessNestedData(item, nodeId, nodeList, connectorList, $"{parentPath}/{keyName}[{index}]", keyName);
                }
                else // Primitive value in array
                {
                    string content = item.ToString();
                    double width = CalculateWidth(content, MAX_NODE_CONTENT_LENGTH, true);
                    double height = CalculateHeight(content, MAX_NODE_CONTENT_LENGTH);
                    var primitiveNode = new Node
                    {
                        ID = nodeId,
                        Width = width,
                        Height = height,
                        Annotations = new DiagramObjectCollection<ShapeAnnotation>
                        {
                            new ShapeAnnotation { Content = content }
                        },
                        Data = new { path = $"{parentPath}/{keyName}[{index}]", title = content, actualdata = content }
                    };
                    nodeList.Add(primitiveNode);
                    connectorList.Add(new Connector
                    {
                        ID = $"connector-{parentId}-{nodeId}",
                        SourceID = parentId,
                        TargetID = nodeId
                    });
                }
                index++;
            }
            return;
        }

        // Process JSON object: separate keys into primitives and non-primitives.
        var propIterator = element.EnumerateObject().ToList();
        var primitives = propIterator.Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array)
                                       .Select(p => p.Name)
                                       .ToList();
        var nonLeaf = propIterator.Where(p => p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array)
                                  .Select(p => p.Name)
                                  .ToList();

        // If there are primitives, merge them into a node
        if (primitives.Count > 0)
        {
            string mergedContent = string.Join("\n", primitives.Select(p => $"{p}: {element.GetProperty(p)}"));
            string leafId = parentId + "-leaf";
            double width = CalculateWidth(mergedContent, MAX_NODE_CONTENT_LENGTH, true);
            double height = CalculateHeight(mergedContent, MAX_NODE_CONTENT_LENGTH);
            var leafNode = new Node
            {
                ID = leafId,
                Width = width,
                Height = height,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = mergedContent }
                },
                Data = new { path = $"{parentPath}.leaf", title = mergedContent, actualdata = mergedContent }
            };
            nodeList.Add(leafNode);
            connectorList.Add(new Connector
            {
                ID = $"connector-{parentId}-{leafId}",
                SourceID = parentId,
                TargetID = leafId
            });
        }

        // Process child (non-primitive) properties
        foreach (var prop in nonLeaf)
        {
            string childId = $"{parentId}-{prop}";
            string displayText = element.GetProperty(prop).ValueKind == JsonValueKind.Array
                                    ? $"{prop} [{GetObjectLength(element.GetProperty(prop))}]"
                                    : prop;
            double width = CalculateWidth(displayText, MAX_NODE_CONTENT_LENGTH, false);
            double height = CalculateHeight(displayText, MAX_NODE_CONTENT_LENGTH);
            var childNode = new Node
            {
                ID = childId,
                Width = width,
                Height = height,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = displayText }
                },
                Data = new { path = $"{parentPath}.{prop}", title = prop, actualdata = prop }
            };
            nodeList.Add(childNode);
            connectorList.Add(new Connector
            {
                ID = $"connector-{parentId}-{childId}",
                SourceID = parentId,
                TargetID = childId
            });
            ProcessNestedData(element.GetProperty(prop), childId, nodeList, connectorList, $"{parentPath}.{prop}", prop);
        }
    }

    // Checks if there are multiple root nodes; if so, add an artificial main root node.
    private static void CheckMultiRoot(List<Node> nodeList, List<Connector> connectorList)
    {
        var nodeIds = nodeList.Select(n => n.ID).ToList();
        var connectedIds = new HashSet<string>(connectorList.Select(c => c.TargetID));
        var roots = nodeIds.Where(id => !connectedIds.Contains(id)).ToList();
        if (roots.Count > 1)
        {
            string mainRootId = "main-root";
            var mainRoot = new Node
            {
                ID = mainRootId,
                Width = 40,
                Height = 40,
                Shape = new BasicShape()
                {
                    Type = NodeShapes.Basic,
                    Shape = NodeBasicShapes.Rectangle,
                    CornerRadius = 10
                },
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = "" }
                },
                Data = new { path = "MainRoot", title = "Main Artificial Root", actualdata = "" }
            };
            nodeList.Add(mainRoot);
            foreach (var r in roots)
            {
                connectorList.Add(new Connector
                {
                    ID = $"connector-{mainRootId}-{r}",
                    SourceID = mainRootId,
                    TargetID = r
                });
            }
        }
    }

    // Returns an approximate width based on character count and fixed pixel multiplier.
    private static double CalculateWidth(string content, int maxLength, bool isLeaf)
    {
        // This is a simplified version that you can refine.
        // Multiply by 8 pixels per character and add extra for icon if not leaf.
        int length = content.Length;
        double baseWidth = Math.Max(150, length * 8);
        if (!isLeaf)
            baseWidth += stableIconHeight;
        return baseWidth;
    }

    // Returns an approximate height based on number of lines.
    private static double CalculateHeight(string content, int maxLength)
    {
        var lines = content.Split('\n');
        int lineCount = lines.Length;
        double baseHeight = 20;
        double lineHeight = 20;
        return baseHeight + lineCount * lineHeight;
    }

    // Returns the child count for a JSON object or array.
    private static int GetObjectLength(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array)
            return 0;
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();
        var keys = element.EnumerateObject().ToList();
        if (keys.Count == 1)
            return keys.Count;
        int nestedCount = 0;
        foreach (var prop in keys)
        {
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                nestedCount++;
        }
        return nestedCount > 0 ? 1 + nestedCount : 1;
    }
}

public class DiagramData
{
    public List<Syncfusion.Blazor.Diagram.Node> Nodes { get; set; } = new List<Syncfusion.Blazor.Diagram.Node>();
    public List<Syncfusion.Blazor.Diagram.Connector> Connectors { get; set; } = new List<Syncfusion.Blazor.Diagram.Connector>();
}
