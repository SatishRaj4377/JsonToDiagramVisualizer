using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Syncfusion.Blazor.Diagram;

public static class JsonDiagramParser
{
    private const double DEFAULT_NODE_WIDTH = 150;
    private const double DEFAULT_NODE_HEIGHT = 50;

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
            var rootNode = new Node
            {
                ID = rootNodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = mergedContent }
                }, 
                AdditionalInfo = new Dictionary<string, object> { { "isLeaf", true } },
                Data = new { path = "Root", title = mergedContent, actualdata = mergedContent, }
            };
            rootNode.AdditionalInfo.Add("mergedContent", mergedContent);
            diagramData.Nodes.Add(rootNode);
        }

        // Process each non-primitive property as a child node.
        foreach (var key in nonLeafNodes)
        {
            string nodeId = key;
            int childCount = GetObjectLength(data.GetProperty(key));

            var annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = key }
            };
            if (childCount > 0)
            {
                annotations.Add(new ShapeAnnotation { Content = "{" + childCount + "}" });
            }

            var childNode = new Node
            {
                ID = nodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = annotations,
                Data = new { path = $"Root.{key}", title = key, actualdata = key, displayContent = new { key = new string[] { key }, displayValue = childCount } },
                AdditionalInfo = new Dictionary<string, object> { { "isLeaf", false } },
            };
            childNode.AdditionalInfo.Add("mergedContent", key + " {" + childCount + "}");
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
                    // Collect primitive fields to merge into a single node (same behavior as current logic)
                    var primitiveList = item.EnumerateObject()
                        .Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array)
                        .Select(p => $"{p.Name}: {p.Value}")
                        .ToList();

                    if (primitiveList.Count > 0)
                    {
                        string mergedContent = string.Join("\n", primitiveList);
                        var mergedNode = new Node
                        {
                            ID = nodeId,
                            Width = DEFAULT_NODE_WIDTH,
                            Height = DEFAULT_NODE_HEIGHT,
                            Annotations = new DiagramObjectCollection<ShapeAnnotation>
                            {
                                new ShapeAnnotation { Content = mergedContent }
                            },
                            AdditionalInfo = new Dictionary<string, object> { { "isLeaf", true } },
                            Data = new { path = $"{parentPath}/{keyName}[{index}]", title = mergedContent, actualdata = mergedContent }
                        };
                        mergedNode.AdditionalInfo.Add("mergedContent", mergedContent);
                        nodeList.Add(mergedNode);
                        connectorList.Add(new Connector
                        {
                            ID = $"connector-{parentId}-{nodeId}",
                            SourceID = parentId,
                            TargetID = nodeId
                        });
                    }

                    // Recursively process any non-primitives (object or array)
                    var children = item.EnumerateObject()
                        .Where(p => p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array)
                        .ToList();

                    foreach (var child in children)
                    {
                        string childId = $"{nodeId}-{child.Name}";
                        string childPath = $"{parentPath}/{keyName}[{index}].{child.Name}";
                        string label = child.Name;

                        int childCount = GetObjectLength(child.Value);
                        var annotations = new DiagramObjectCollection<ShapeAnnotation>
                        {
                            new ShapeAnnotation { Content = label }
                        };
                        if (childCount > 0)
                        {
                            annotations.Add(new ShapeAnnotation { Content = "{" + childCount + "}" });
                        }
                        var childNode = new Node
                        {
                            ID = childId,
                            Width = DEFAULT_NODE_WIDTH,
                            Height = DEFAULT_NODE_HEIGHT,
                            Annotations = annotations,
                            AdditionalInfo = new Dictionary<string, object> { { "isLeaf", false } },
                            Data = new { path = childPath, title = label, actualdata = label }
                        };
                        childNode.AdditionalInfo.Add("mergedContent", label + " {" + childCount + "}");
                        nodeList.Add(childNode);
                        connectorList.Add(new Connector
                        {
                            ID = $"connector-{nodeId}-{childId}",
                            SourceID = nodeId,
                            TargetID = childId
                        });

                        // Recursively process nested object
                        ProcessNestedData(child.Value, childId, nodeList, connectorList, childPath, child.Name);
                    }
                }
                else // Primitive value in array
                {
                    string content = item.ToString();
                    var primitiveNode = new Node
                    {
                        ID = nodeId,
                        Width = DEFAULT_NODE_WIDTH,
                        Height = DEFAULT_NODE_HEIGHT,
                        Annotations = new DiagramObjectCollection<ShapeAnnotation>
                        {
                            new ShapeAnnotation { Content = content }
                        },
                        AdditionalInfo = new Dictionary<string, object> { { "isLeaf", true } },
                        Data = new { path = $"{parentPath}/{keyName}[{index}]", title = content, actualdata = content }
                    };
                    primitiveNode.AdditionalInfo.Add("mergedContent", content);
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
            var leafNode = new Node
            {
                ID = leafId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = new DiagramObjectCollection<ShapeAnnotation>
                {
                    new ShapeAnnotation { Content = mergedContent }
                },
                AdditionalInfo = new Dictionary<string, object> { { "isLeaf", true } },
                Data = new { path = $"{parentPath}.leaf", title = mergedContent, actualdata = mergedContent }
            };
            leafNode.AdditionalInfo.Add("mergedContent", mergedContent);
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

            int childCount = GetObjectLength(element.GetProperty(prop));
            var annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = prop }
            };
            if (childCount > 0)
            {
                annotations.Add(new ShapeAnnotation { Content = "{" + childCount + "}" });
            }

            var childNode = new Node
            {
                ID = childId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = annotations,
                AdditionalInfo = new Dictionary<string, object> { { "isLeaf", false } },
                Data = new { path = $"{parentPath}.{prop}", title = prop, actualdata = prop }
            };
            childNode.AdditionalInfo.Add("mergedContent", prop + " {" + childCount + "}");
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
    public List<Node> Nodes { get; set; } = new List<Syncfusion.Blazor.Diagram.Node>();
    public List<Connector> Connectors { get; set; } = new List<Syncfusion.Blazor.Diagram.Connector>();
}
