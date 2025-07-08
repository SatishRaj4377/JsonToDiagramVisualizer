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

    /// <summary>
    /// Processes input JSON string and returns diagram data.
    /// </summary>
    /// <param name="jsonData">JSON string to parse</param>
    /// <returns>DiagramData containing nodes and connectors</returns>
    public static DiagramData ProcessData(string jsonData)
    {
        var parsedDiagramData = new DiagramData();

        using var doc = JsonDocument.Parse(jsonData);
        var inputJsonData = doc.RootElement;

        if (!IsValidJsonData(inputJsonData))
        {
            return parsedDiagramData;
        }

        var processedData = PreprocessJsonData(inputJsonData);
        var processedJson = processedData.processedJson;
        var rootNodeIdentifier = processedData.rootNodeIdentifier;
        var shouldSkipEmptyRoot = processedData.shouldSkipEmptyRoot;

        var categorizedKeys = CategorizeObjectKeys(processedJson);
        var nestedObjectKeys = categorizedKeys.nestedObjectKeys;
        var primitiveValueKeys = categorizedKeys.primitiveValueKeys;

        var isRootNodeCreated = ProcessRootNode(
            processedJson,
            primitiveValueKeys,
            rootNodeIdentifier,
            shouldSkipEmptyRoot,
            parsedDiagramData
        );

        ProcessNestedObjectKeys(
            processedJson,
            nestedObjectKeys,
            rootNodeIdentifier,
            isRootNodeCreated,
            parsedDiagramData
        );

        HandleMultipleRootNodes(parsedDiagramData, shouldSkipEmptyRoot, isRootNodeCreated);

        return parsedDiagramData;
    }

    // Validate if the input JSON data is valid for processing
    private static bool IsValidJsonData(JsonElement inputJsonData)
    {
        return inputJsonData.ValueKind == JsonValueKind.Object &&
               inputJsonData.EnumerateObject().Any();
    }

    // Preprocess JSON data to handle single root key scenarios
    private static (JsonElement processedJson, string rootNodeIdentifier, bool shouldSkipEmptyRoot) PreprocessJsonData(JsonElement inputJsonData)
    {
        string rootNodeIdentifier = "root";
        JsonElement processedJson = inputJsonData;
        bool shouldSkipEmptyRoot = false;

        var jsonObjectKeys = inputJsonData.EnumerateObject().ToList();

        if (jsonObjectKeys.Count == 1)
        {
            var singleRootKey = jsonObjectKeys[0].Name;
            var rootValue = jsonObjectKeys[0].Value;

            if (IsEmptyOrWhitespace(singleRootKey) && rootValue.ValueKind == JsonValueKind.Object)
            {
                shouldSkipEmptyRoot = true;
                processedJson = rootValue;
            }
            else if (!IsEmptyOrWhitespace(singleRootKey) && rootValue.ValueKind == JsonValueKind.Object)
            {
                rootNodeIdentifier = singleRootKey;
            }
        }

        return (processedJson, rootNodeIdentifier, shouldSkipEmptyRoot);
    }

    // Categorize object keys into primitive and nested object properties
    private static (List<string> nestedObjectKeys, List<string> primitiveValueKeys) CategorizeObjectKeys(JsonElement jsonData)
    {
        var nestedObjectKeys = new List<string>();
        var primitiveValueKeys = new List<string>();

        foreach (var prop in jsonData.EnumerateObject())
        {
            var keyName = prop.Name;
            var keyValue = prop.Value;

            if (keyValue.ValueKind == JsonValueKind.Object || keyValue.ValueKind == JsonValueKind.Array)
            {
                nestedObjectKeys.Add(keyName);
            }
            else
            {
                primitiveValueKeys.Add(keyName);
            }
        }

        return (nestedObjectKeys, primitiveValueKeys);
    }

    // Process root node creation for primitive properties
    private static bool ProcessRootNode(JsonElement jsonData, List<string> primitiveValueKeys, string rootNodeIdentifier, bool shouldSkipEmptyRoot, DiagramData parsedDiagramData)
    {
        if (primitiveValueKeys.Count == 0)
        {
            return false;
        }

        var finalRootId = shouldSkipEmptyRoot ? "DataRoot" : ConvertUnderScoreToPascalCase(rootNodeIdentifier);
        var primitiveLeafAnnotations = CreatePrimitiveAnnotations(jsonData, primitiveValueKeys);
        var combinedPrimitiveContent = CreateCombinedPrimitiveContent(jsonData, primitiveValueKeys);

        var rootLeafNode = new Node
        {
            ID = finalRootId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = primitiveLeafAnnotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = true,
                ["mergedContent"] = combinedPrimitiveContent
            },
            Data = new { path = "Root", title = combinedPrimitiveContent, actualdata = combinedPrimitiveContent }
        };

        parsedDiagramData.Nodes.Add(rootLeafNode);
        return true;
    }

    // Create annotations for primitive key-value pairs
    private static DiagramObjectCollection<ShapeAnnotation> CreatePrimitiveAnnotations(JsonElement jsonData, List<string> primitiveKeys)
    {
        var annotations = new DiagramObjectCollection<ShapeAnnotation>();

        foreach (var keyName in primitiveKeys)
        {
            var property = jsonData.EnumerateObject().FirstOrDefault(p => p.Name == keyName);
            var formattedValue = FormatValue(property.Value.ToString());

            annotations.Add(new ShapeAnnotation { ID = $"Key_{keyName}", Content = $"{keyName}:" });
            annotations.Add(new ShapeAnnotation { ID = $"Value_{keyName}", Content = formattedValue });
        }

        return annotations;
    }

    // Create combined content string for primitive properties
    private static string CreateCombinedPrimitiveContent(JsonElement jsonData, List<string> primitiveKeys)
    {
        var lines = new List<string>();

        foreach (var keyName in primitiveKeys)
        {
            var property = jsonData.EnumerateObject().FirstOrDefault(p => p.Name == keyName);
            var value = property.Value.ToString();
            lines.Add($"{keyName}: {value}");
        }

        return string.Join("\n", lines);
    }

    // Process nested object keys and create their nodes
    private static void ProcessNestedObjectKeys(JsonElement jsonData, List<string> nestedObjectKeys, string rootNodeIdentifier, bool isRootNodeCreated, DiagramData parsedDiagramData)
    {
        foreach (var nestedKeyName in nestedObjectKeys)
        {
            var property = jsonData.EnumerateObject().FirstOrDefault(p => p.Name == nestedKeyName);
            if (IsEmpty(property.Value)) continue;

            var nestedNodeId = CreateNestedObjectNode(property.Value, nestedKeyName, parsedDiagramData);

            if (isRootNodeCreated)
            {
                CreateConnector(rootNodeIdentifier, nestedNodeId, parsedDiagramData);
            }

            ProcessNestedData(
                property.Value,
                nestedNodeId,
                parsedDiagramData.Nodes,
                parsedDiagramData.Connectors,
                $"Root.{nestedKeyName}",
                nestedKeyName
            );
        }
    }

    // Create a nested object node and return its ID
    private static string CreateNestedObjectNode(JsonElement nestedValue, string nestedKeyName, DiagramData parsedDiagramData)
    {
        var nestedNodeId = ConvertUnderScoreToPascalCase(nestedKeyName);
        var nestedChildCount = GetObjectLength(nestedValue);
        var nestedNodeAnnotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { Content = nestedKeyName }
        };

        if (nestedChildCount > 0)
        {
            nestedNodeAnnotations.Add(new ShapeAnnotation { Content = $"{{{nestedChildCount}}}" });
        }

        var mergedContent = $"{nestedKeyName}  {{{nestedChildCount}}}";
        var nestedObjectNode = new Node
        {
            ID = nestedNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = nestedNodeAnnotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = mergedContent
            },
            Data = new { path = $"Root.{nestedKeyName}", title = nestedKeyName, actualdata = nestedKeyName }
        };

        parsedDiagramData.Nodes.Add(nestedObjectNode);
        return nestedNodeId;
    }

    // Create a connector between two nodes
    private static void CreateConnector(string sourceId, string targetId, DiagramData parsedDiagramData)
    {
        parsedDiagramData.Connectors.Add(new Connector
        {
            ID = $"connector-{sourceId}-{targetId}",
            SourceID = sourceId,
            TargetID = targetId
        });
    }

    // Handle multiple root nodes scenario
    private static void HandleMultipleRootNodes(DiagramData parsedDiagramData, bool shouldSkipEmptyRoot, bool isRootNodeCreated)
    {
        var hasMultipleRoots = HasMultipleRoots(parsedDiagramData.Nodes, parsedDiagramData.Connectors);

        if ((shouldSkipEmptyRoot || hasMultipleRoots) && !isRootNodeCreated)
        {
            CheckMultiRoot(parsedDiagramData.Nodes, parsedDiagramData.Connectors);
        }
    }

    // Recursively processes nested objects/arrays
    private static void ProcessNestedData(JsonElement nestedElement, string parentNodeId, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        if (nestedElement.ValueKind == JsonValueKind.Array)
        {
            ProcessArrayElements(nestedElement, parentNodeId, diagramNodes, diagramConnectors, currentPath, parentKeyName);
            return;
        }

        if (nestedElement.ValueKind == JsonValueKind.Object)
        {
            ProcessObjectElements(nestedElement, parentNodeId, diagramNodes, diagramConnectors, currentPath);
        }
    }

    // Process array elements and create corresponding nodes
    private static void ProcessArrayElements(JsonElement arrayElement, string parentNodeId, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        if (IsEmpty(arrayElement)) return;

        int arrayIndex = 0;
        foreach (var arrayItem in arrayElement.EnumerateArray())
        {
            if (arrayItem.ValueKind == JsonValueKind.Null)
            {
                arrayIndex++;
                continue;
            }

            var arrayItemNodeId = ConvertUnderScoreToPascalCase($"{parentNodeId}-{arrayIndex}");

            if (IsComplexArrayItem(arrayItem))
            {
                ProcessComplexArrayItem(
                    arrayItem,
                    arrayItemNodeId,
                    parentNodeId,
                    arrayIndex,
                    diagramNodes,
                    diagramConnectors,
                    currentPath,
                    parentKeyName
                );
            }
            else
            {
                ProcessPrimitiveArrayItem(
                    arrayItem,
                    arrayItemNodeId,
                    parentNodeId,
                    arrayIndex,
                    diagramNodes,
                    diagramConnectors,
                    currentPath,
                    parentKeyName
                );
            }

            arrayIndex++;
        }
    }

    // Check if array item is a complex object
    private static bool IsComplexArrayItem(JsonElement arrayItem)
    {
        return arrayItem.ValueKind == JsonValueKind.Object;
    }

    // Process complex array item (object)
    private static void ProcessComplexArrayItem(JsonElement arrayItem, string arrayItemNodeId, string parentNodeId, int arrayIndex, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        var objectPropertyEntries = arrayItem.EnumerateObject().ToList();
        var primitivePropertyEntries = objectPropertyEntries.Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array).ToList();
        var nestedPropertyEntries = objectPropertyEntries.Where(p => (p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array) && !IsEmpty(p.Value)).ToList();

        var requiresIntermediateNode = primitivePropertyEntries.Count > 0 || nestedPropertyEntries.Count > 1;

        if (requiresIntermediateNode)
        {
            CreateIntermediateArrayNode(
                arrayItem,
                arrayItemNodeId,
                parentNodeId,
                arrayIndex,
                primitivePropertyEntries,
                nestedPropertyEntries,
                diagramNodes,
                diagramConnectors,
                currentPath,
                parentKeyName
            );
        }
        else if (nestedPropertyEntries.Count == 1)
        {
            CreateDirectArrayNode(
                nestedPropertyEntries[0],
                arrayItemNodeId,
                parentNodeId,
                arrayIndex,
                diagramNodes,
                diagramConnectors,
                currentPath,
                parentKeyName
            );
        }
    }

    // Create intermediate node for complex array items
    private static void CreateIntermediateArrayNode(JsonElement arrayItem, string arrayItemNodeId, string parentNodeId, int arrayIndex, List<JsonProperty> primitivePropertyEntries, List<JsonProperty> nestedPropertyEntries, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        string intermediateNodeContent;
        bool isIntermediateLeafNode;
        DiagramObjectCollection<ShapeAnnotation> intermediateNodeAnnotations;

        if (primitivePropertyEntries.Count > 0)
        {
            isIntermediateLeafNode = true;
            intermediateNodeAnnotations = CreateArrayItemPrimitiveAnnotations(primitivePropertyEntries, arrayItemNodeId);
            var contentLines = primitivePropertyEntries.Select(p => $"{p.Name}: {p.Value}").ToList();
            intermediateNodeContent = string.Join("\n", contentLines);
        }
        else
        {
            isIntermediateLeafNode = false;
            intermediateNodeContent = $"Item {arrayIndex}";
            intermediateNodeAnnotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = intermediateNodeContent }
            };
        }

        var arrayItemIntermediateNode = new Node
        {
            ID = arrayItemNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = intermediateNodeAnnotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = isIntermediateLeafNode,
                ["mergedContent"] = intermediateNodeContent
            },
            Data = new { path = $"{currentPath}[{arrayIndex}]", title = intermediateNodeContent, actualdata = intermediateNodeContent }
        };

        diagramNodes.Add(arrayItemIntermediateNode);
        diagramConnectors.Add(new Connector
        {
            ID = $"connector-{parentNodeId}-{arrayItemNodeId}",
            SourceID = parentNodeId,
            TargetID = arrayItemNodeId
        });

        ProcessNestedPropertyEntries(
            nestedPropertyEntries,
            arrayItemNodeId,
            arrayIndex,
            diagramNodes,
            diagramConnectors,
            currentPath,
            parentKeyName
        );
    }

    // Create annotations for array item primitive properties
    private static DiagramObjectCollection<ShapeAnnotation> CreateArrayItemPrimitiveAnnotations(List<JsonProperty> primitivePropertyEntries, string arrayItemNodeId)
    {
        var annotations = new DiagramObjectCollection<ShapeAnnotation>();

        foreach (var prop in primitivePropertyEntries)
        {
            var propertyKey = prop.Name;
            var formattedPropertyValue = FormatValue(prop.Value.ToString());

            annotations.Add(new ShapeAnnotation { ID = $"Key_{arrayItemNodeId}_{propertyKey}", Content = $"{propertyKey}:" });
            annotations.Add(new ShapeAnnotation { ID = $"Value_{arrayItemNodeId}_{propertyKey}", Content = formattedPropertyValue });
        }

        return annotations;
    }

    // Process nested property entries for array items
    private static void ProcessNestedPropertyEntries(List<JsonProperty> nestedPropertyEntries, string arrayItemNodeId, int arrayIndex, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        foreach (var nestedProperty in nestedPropertyEntries)
        {
            var nestedPropertyKey = nestedProperty.Name;
            var nestedPropertyValue = nestedProperty.Value;
            var nestedPropertyNodeId = ConvertUnderScoreToPascalCase($"{arrayItemNodeId}-{nestedPropertyKey}");
            var nestedPropertyChildCount = GetObjectLength(nestedPropertyValue);
            var nestedPropertyAnnotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = nestedPropertyKey }
            };

            if (nestedPropertyChildCount > 0)
            {
                nestedPropertyAnnotations.Add(new ShapeAnnotation { Content = $"{{{nestedPropertyChildCount}}}" });
            }

            var mergedContent = $"{nestedPropertyKey}  {{{nestedPropertyChildCount}}}";
            var nestedPropertyNode = new Node
            {
                ID = nestedPropertyNodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = nestedPropertyAnnotations,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = false,
                    ["mergedContent"] = mergedContent
                },
                Data = new { path = $"{currentPath}[{arrayIndex}].{nestedPropertyKey}", title = nestedPropertyKey, actualdata = nestedPropertyKey }
            };

            diagramNodes.Add(nestedPropertyNode);
            diagramConnectors.Add(new Connector
            {
                ID = $"connector-{arrayItemNodeId}-{nestedPropertyNodeId}",
                SourceID = arrayItemNodeId,
                TargetID = nestedPropertyNodeId
            });

            ProcessNestedData(
                nestedPropertyValue,
                nestedPropertyNodeId,
                diagramNodes,
                diagramConnectors,
                $"{currentPath}[{arrayIndex}].{nestedPropertyKey}",
                nestedPropertyKey
            );
        }
    }

    // Create direct node for single nested object in array
    private static void CreateDirectArrayNode(JsonProperty nestedEntry, string arrayItemNodeId, string parentNodeId, int arrayIndex, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        var singleNestedKey = nestedEntry.Name;
        var singleNestedValue = nestedEntry.Value;
        var directNestedNodeId = ConvertUnderScoreToPascalCase($"{arrayItemNodeId}-{singleNestedKey}");
        var directNestedChildCount = GetObjectLength(singleNestedValue);
        var directNestedAnnotations = new DiagramObjectCollection<ShapeAnnotation>
        {
            new ShapeAnnotation { Content = singleNestedKey }
        };

        if (directNestedChildCount > 0)
        {
            directNestedAnnotations.Add(new ShapeAnnotation { Content = $"{{{directNestedChildCount}}}" });
        }

        var mergedContent = $"{singleNestedKey}  {{{directNestedChildCount}}}";
        var directConnectionNode = new Node
        {
            ID = directNestedNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = directNestedAnnotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = false,
                ["mergedContent"] = mergedContent
            },
            Data = new { path = $"{currentPath}[{arrayIndex}].{singleNestedKey}", title = singleNestedKey, actualdata = singleNestedKey }
        };

        diagramNodes.Add(directConnectionNode);
        diagramConnectors.Add(new Connector
        {
            ID = $"connector-{parentNodeId}-{directNestedNodeId}",
            SourceID = parentNodeId,
            TargetID = directNestedNodeId
        });

        ProcessNestedData(
            singleNestedValue,
            directNestedNodeId,
            diagramNodes,
            diagramConnectors,
            $"{currentPath}[{arrayIndex}].{singleNestedKey}",
            singleNestedKey
        );
    }

    // Process primitive array item
    private static void ProcessPrimitiveArrayItem(JsonElement arrayItem, string arrayItemNodeId, string parentNodeId, int arrayIndex, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath, string parentKeyName)
    {
        var primitiveArrayContent = FormatValue(arrayItem.ToString());
        var primitiveArrayNode = new Node
        {
            ID = arrayItemNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { ID = $"Value_{parentKeyName}", Content = primitiveArrayContent }
            },
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = true,
                ["mergedContent"] = primitiveArrayContent
            },
            Data = new { path = $"{currentPath}[{arrayIndex}]", title = primitiveArrayContent, actualdata = primitiveArrayContent }
        };

        diagramNodes.Add(primitiveArrayNode);
        diagramConnectors.Add(new Connector
        {
            ID = $"connector-{parentNodeId}-{arrayItemNodeId}",
            SourceID = parentNodeId,
            TargetID = arrayItemNodeId
        });
    }

    // Process object elements and create corresponding nodes
    private static void ProcessObjectElements(JsonElement nestedElement, string parentNodeId, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath)
    {
        var objectPropertyEntries = nestedElement.EnumerateObject().ToList();
        var primitiveObjectKeys = objectPropertyEntries
            .Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array)
            .Select(p => p.Name)
            .ToList();
        var nestedObjectKeys = objectPropertyEntries
            .Where(p => (p.Value.ValueKind == JsonValueKind.Object || p.Value.ValueKind == JsonValueKind.Array))
            .Select(p => p.Name)
            .ToList();

        if (primitiveObjectKeys.Count > 0)
        {
            CreateLeafNodeForPrimitives(primitiveObjectKeys, nestedElement, parentNodeId, diagramNodes, diagramConnectors, currentPath);
        }

        ProcessNestedObjectProperties(nestedObjectKeys, nestedElement, parentNodeId, diagramNodes, diagramConnectors, currentPath);
    }

    // Create leaf node for primitive properties in nested object
    private static void CreateLeafNodeForPrimitives(List<string> primitiveObjectKeys, JsonElement nestedElement, string parentNodeId, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath)
    {
        var primitiveLeafNodeId = ConvertUnderScoreToPascalCase($"{parentNodeId}-leaf");
        var primitiveObjectAnnotations = new DiagramObjectCollection<ShapeAnnotation>();
        var contentLines = new List<string>();

        foreach (var primitiveKey in primitiveObjectKeys)
        {
            var property = nestedElement.EnumerateObject().FirstOrDefault(p => p.Name == primitiveKey);
            var primitiveRawValue = FormatValue(property.Value.ToString());

            primitiveObjectAnnotations.Add(new ShapeAnnotation { ID = $"Key_{primitiveLeafNodeId}_{primitiveKey}", Content = $"{primitiveKey}:" });
            primitiveObjectAnnotations.Add(new ShapeAnnotation { ID = $"Value_{primitiveLeafNodeId}_{primitiveKey}", Content = primitiveRawValue });
            contentLines.Add($"{primitiveKey}: {property.Value}");
        }

        var combinedPrimitiveObjectContent = string.Join("\n", contentLines);

        var primitiveObjectLeafNode = new Node
        {
            ID = primitiveLeafNodeId,
            Width = DEFAULT_NODE_WIDTH,
            Height = DEFAULT_NODE_HEIGHT,
            Annotations = primitiveObjectAnnotations,
            AdditionalInfo = new Dictionary<string, object>
            {
                ["isLeaf"] = true,
                ["mergedContent"] = combinedPrimitiveObjectContent
            },
            Data = new { path = $"{currentPath}.leaf", title = combinedPrimitiveObjectContent, actualdata = combinedPrimitiveObjectContent }
        };

        diagramNodes.Add(primitiveObjectLeafNode);
        diagramConnectors.Add(new Connector
        {
            ID = $"connector-{parentNodeId}-{primitiveLeafNodeId}",
            SourceID = parentNodeId,
            TargetID = primitiveLeafNodeId
        });
    }

    // Process nested object properties recursively
    private static void ProcessNestedObjectProperties(List<string> nestedObjectKeys, JsonElement nestedElement, string parentNodeId, List<Node> diagramNodes, List<Connector> diagramConnectors, string currentPath)
    {
        foreach (var nestedObjectProperty in nestedObjectKeys)
        {
            var property = nestedElement.EnumerateObject().FirstOrDefault(p => p.Name == nestedObjectProperty);
            var nestedObjectPropertyValue = property.Value;

            if (IsEmpty(nestedObjectPropertyValue)) continue;

            var nestedObjectPropertyChildCount = GetObjectLength(nestedObjectPropertyValue);
            var nestedObjectPropertyNodeId = ConvertUnderScoreToPascalCase($"{parentNodeId}-{nestedObjectProperty}");
            var nestedObjectPropertyAnnotations = new DiagramObjectCollection<ShapeAnnotation>
            {
                new ShapeAnnotation { Content = nestedObjectProperty }
            };

            if (nestedObjectPropertyChildCount > 0)
            {
                nestedObjectPropertyAnnotations.Add(new ShapeAnnotation { Content = $"{{{nestedObjectPropertyChildCount}}}" });
            }

            var mergedContent = $"{nestedObjectProperty}  {{{nestedObjectPropertyChildCount}}}";
            var nestedObjectPropertyNode = new Node
            {
                ID = nestedObjectPropertyNodeId,
                Width = DEFAULT_NODE_WIDTH,
                Height = DEFAULT_NODE_HEIGHT,
                Annotations = nestedObjectPropertyAnnotations,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["isLeaf"] = false,
                    ["mergedContent"] = mergedContent
                },
                Data = new { path = $"{currentPath}.{nestedObjectProperty}", title = nestedObjectProperty, actualdata = nestedObjectProperty }
            };

            diagramNodes.Add(nestedObjectPropertyNode);
            diagramConnectors.Add(new Connector
            {
                ID = $"connector-{parentNodeId}-{nestedObjectPropertyNodeId}",
                SourceID = parentNodeId,
                TargetID = nestedObjectPropertyNodeId
            });

            ProcessNestedData(
                nestedObjectPropertyValue,
                nestedObjectPropertyNodeId,
                diagramNodes,
                diagramConnectors,
                $"{currentPath}.{nestedObjectProperty}",
                nestedObjectProperty
            );
        }
    }

    // Check if there are multiple root nodes (nodes without parents)
    private static bool HasMultipleRoots(List<Node> diagramNodes, List<Connector> diagramConnectors)
    {
        var allNodeIds = diagramNodes.Select(n => n.ID).ToList();
        var connectedNodeIds = new HashSet<string>(diagramConnectors.Select(c => c.TargetID));
        var rootNodeIds = allNodeIds.Where(nodeId => !connectedNodeIds.Contains(nodeId)).ToList();
        return rootNodeIds.Count > 1;
    }

    // Adds an artificial main root if multiple roots exist
    private static void CheckMultiRoot(List<Node> diagramNodes, List<Connector> diagramConnectors)
    {
        var allNodeIds = diagramNodes.Select(n => n.ID).ToList();
        var connectedNodeIds = new HashSet<string>(diagramConnectors.Select(c => c.TargetID));
        var rootNodeIds = allNodeIds.Where(nodeId => !connectedNodeIds.Contains(nodeId)).ToList();

        if (rootNodeIds.Count > 1)
        {
            const string artificialMainRootId = "main-root";
            var artificialMainRootNode = new Node
            {
                ID = artificialMainRootId,
                Width = 40,
                Height = 40,
                Annotations = new DiagramObjectCollection<ShapeAnnotation> { new ShapeAnnotation { Content = "" } },
                AdditionalInfo = new Dictionary<string, object> { ["isLeaf"] = false },
                Data = new { path = "MainRoot", title = "", actualdata = "" }
            };

            diagramNodes.Add(artificialMainRootNode);
            foreach (var rootNodeId in rootNodeIds)
            {
                diagramConnectors.Add(new Connector
                {
                    ID = $"connector-{artificialMainRootId}-{rootNodeId}",
                    SourceID = artificialMainRootId,
                    TargetID = rootNodeId
                });
            }
        }
    }

    // Returns count of children for objects/arrays
    private static int GetObjectLength(JsonElement targetElement)
    {
        if (targetElement.ValueKind == JsonValueKind.Array)
            return targetElement.GetArrayLength();

        if (targetElement.ValueKind != JsonValueKind.Object)
            return 0;

        var elementPropertyEntries = targetElement.EnumerateObject().ToList();
        var primitivePropertyEntries = elementPropertyEntries.Where(p => p.Value.ValueKind != JsonValueKind.Object && p.Value.ValueKind != JsonValueKind.Array).ToList();
        var arrayPropertyEntries = elementPropertyEntries.Where(p => p.Value.ValueKind == JsonValueKind.Array).ToList();
        var objectPropertyEntries = elementPropertyEntries.Where(p => p.Value.ValueKind == JsonValueKind.Object).ToList();

        return (primitivePropertyEntries.Count > 0 ? 1 : 0) + arrayPropertyEntries.Count + objectPropertyEntries.Count;
    }

    // Converts strings from underscore/hyphen to PascalCase segments
    private static string ConvertUnderScoreToPascalCase(string inputString)
    {
        if (string.IsNullOrEmpty(inputString))
            return inputString;

        var parts = inputString.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
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

    // Checks if a value is an empty array, an empty object, or not set
    private static bool IsEmpty(JsonElement valueToCheck)
    {
        if (valueToCheck.ValueKind == JsonValueKind.Array)
            return valueToCheck.GetArrayLength() == 0;

        if (valueToCheck.ValueKind == JsonValueKind.Object)
            return !valueToCheck.EnumerateObject().Any();

        return false;
    }

    // Helper method to check if a string is empty or contains only whitespace
    private static bool IsEmptyOrWhitespace(string stringToCheck)
    {
        return string.IsNullOrWhiteSpace(stringToCheck);
    }

    // Format value based on type (boolean, numeric, or string)
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