using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cel.Compiled.Compiler;
using Cel.Compiled.Parser;
using Xunit;

namespace Cel.Compiled.Tests;

public class ContainerTests
{
    private static object? Eval(string expression)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<object, object?>(ast);
        return compiled(new object());
    }

    private static object? Eval<TContext>(string expression, TContext context)
    {
        var ast = CelParser.Parse(expression);
        var compiled = CelCompiler.Compile<TContext, object?>(ast);
        return compiled(context);
    }

    private sealed class ListContext
    {
        public List<long> Numbers { get; init; } = new();
        public ArrayList LegacyNumbers { get; init; } = new();
        public Dictionary<string, long> Values { get; init; } = new();
    }

    [Fact]
    public void EmptyList()
    {
        var result = Eval("[]");
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<IList>(result);
        Assert.Empty(list);
    }

    [Fact]
    public void IntList()
    {
        var result = Eval("[1, 2, 3]");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void UintList()
    {
        var result = Eval("[1u, 2u]");
        var list = Assert.IsAssignableFrom<IList<ulong>>(result);
        Assert.Equal(new ulong[] { 1, 2 }, list);
    }

    [Fact]
    public void DoubleList()
    {
        var result = Eval("[1.0, 2.5]");
        var list = Assert.IsAssignableFrom<IList<double>>(result);
        Assert.Equal(new double[] { 1.0, 2.5 }, list);
    }

    [Fact]
    public void StringList()
    {
        var result = Eval("['a', 'b', 'c']");
        var list = Assert.IsAssignableFrom<IList<string>>(result);
        Assert.Equal(new string[] { "a", "b", "c" }, list);
    }

    [Fact]
    public void BoolList()
    {
        var result = Eval("[true, false]");
        var list = Assert.IsAssignableFrom<IList<bool>>(result);
        Assert.Equal(new bool[] { true, false }, list);
    }

    [Fact]
    public void MixedTypeList()
    {
        var result = Eval("[1, 'a']");
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal("a", list[1]);
    }

    [Fact]
    public void MixedNumericList()
    {
        var result = Eval("[1, 1u, 1.0]");
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Equal(3, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal(1UL, list[1]);
        Assert.Equal(1.0, list[2]);
    }

    [Fact]
    public void NullList()
    {
        var result = Eval("[null]");
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Single(list);
        Assert.Null(list[0]);
    }

    [Fact]
    public void MixedNullList()
    {
        var result = Eval("[1, null]");
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Null(list[1]);
    }

    [Fact]
    public void NestedList()
    {
        var result = Eval("[[1, 2], [3, 4]]");
        var list = Assert.IsAssignableFrom<IList>(result);
        Assert.Equal(2, list.Count);
        
        var inner1 = Assert.IsAssignableFrom<IList<long>>(list[0]);
        Assert.Equal(new long[] { 1, 2 }, inner1);
        
        var inner2 = Assert.IsAssignableFrom<IList<long>>(list[1]);
        Assert.Equal(new long[] { 3, 4 }, inner2);
    }

    [Fact]
    public void SizeIntegration()
    {
        var result = Eval("size([1, 2, 3])");
        Assert.Equal(3L, result);
    }

    [Fact]
    public void BytesSizeIntegration()
    {
        var result = Eval("size(b\"abc\")");
        Assert.Equal(3L, result);
    }

    [Fact]
    public void TrailingComma()
    {
        var result = Eval("[1, 2,]");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal(2L, list[1]);
    }

    [Fact]
    public void EmptyMap()
    {
        var result = Eval("{}");
        var map = Assert.IsAssignableFrom<IDictionary>(result);
        Assert.Empty(map);
    }

    [Fact]
    public void StringLongMap()
    {
        var result = Eval("{\"a\": 1, \"b\": 2}");
        var map = Assert.IsAssignableFrom<IDictionary<string, long>>(result);
        Assert.Equal(2, map.Count);
        Assert.Equal(1L, map["a"]);
        Assert.Equal(2L, map["b"]);
    }

    [Fact]
    public void IntStringMap()
    {
        var result = Eval("{1: \"one\", 2: \"two\"}");
        var map = Assert.IsAssignableFrom<IDictionary<long, string>>(result);
        Assert.Equal(2, map.Count);
        Assert.Equal("one", map[1L]);
        Assert.Equal("two", map[2L]);
    }

    [Fact]
    public void BoolStringMap()
    {
        var result = Eval("{true: \"yes\", false: \"no\"}");
        var map = Assert.IsAssignableFrom<IDictionary<bool, string>>(result);
        Assert.Equal(2, map.Count);
        Assert.Equal("yes", map[true]);
        Assert.Equal("no", map[false]);
    }

    [Fact]
    public void UintStringMap()
    {
        var result = Eval("{1u: \"x\"}");
        var map = Assert.IsAssignableFrom<IDictionary<ulong, string>>(result);
        Assert.Single(map);
        Assert.Equal("x", map[1UL]);
    }

    [Fact]
    public void MixedValueTypeMap()
    {
        var result = Eval("{\"a\": 1, \"b\": \"two\"}");
        var map = Assert.IsAssignableFrom<IDictionary<string, object?>>(result);
        Assert.Equal(1L, map["a"]);
        Assert.Equal("two", map["b"]);
    }

    [Fact]
    public void MixedKeyTypeMap()
    {
        var result = Eval("{1: \"int\", 1u: \"uint\"}");
        var map = Assert.IsAssignableFrom<IDictionary<object, string>>(result);
        Assert.Equal("int", map[1L]);
        Assert.Equal("uint", map[1UL]);
    }

    [Fact]
    public void NullValueMap()
    {
        var result = Eval("{\"a\": null}");
        var map = Assert.IsAssignableFrom<IDictionary<string, object?>>(result);
        Assert.Null(map["a"]);
    }

    [Fact]
    public void MixedNullValueMap()
    {
        var result = Eval("{\"a\": 1, \"b\": null}");
        var map = Assert.IsAssignableFrom<IDictionary<string, object?>>(result);
        Assert.Equal(1L, map["a"]);
        Assert.Null(map["b"]);
    }

    [Fact]
    public void InvalidKeyTypeDoubleThrows()
    {
        Assert.Throws<CelCompilationException>(() => Eval("{1.0: \"x\"}"));
    }

    [Fact]
    public void MapSizeIntegration()
    {
        var result = Eval("size({\"a\": 1, \"b\": 2})");
        Assert.Equal(2L, result);
    }

    [Fact]
    public void JsonElementObjectSizeIntegration()
    {
        using var document = JsonDocument.Parse("""{"obj":{"a":1,"b":2,"c":3}}""");
        var result = Eval("size(obj)", document.RootElement);
        Assert.Equal(3L, result);
    }

    [Fact]
    public void MapTrailingComma()
    {
        var result = Eval("{\"a\": 1,}");
        var map = Assert.IsAssignableFrom<IDictionary<string, long>>(result);
        Assert.Single(map);
        Assert.Equal(1L, map["a"]);
    }

    [Fact]
    public void ArrayBackedListLiteralIndex()
    {
        var result = Eval("[1, 2, 3][1]");
        Assert.Equal(2L, result);
    }

    [Fact]
    public void ArrayBackedListLiteralMembership()
    {
        var result = Eval("2 in [1, 2, 3]");
        Assert.Equal(true, result);
    }

    [Fact]
    public void ArrayBackedListLiteralMembershipUsesCelEquality()
    {
        var result = Eval("1u in [1, 2, 3]");
        Assert.Equal(true, result);
    }

    [Fact]
    public void MixedListMembershipUsesCelEquality()
    {
        var result = Eval("1.0 in [1, 2u, 'three']");
        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonElementStringMembershipUsesCelEquality()
    {
        using var document = JsonDocument.Parse("""{"Value":"Value"}""");
        var result = Eval("""Value in ["Value"]""", document.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void GenericListIndex()
    {
        var result = Eval("Numbers[1]", new ListContext { Numbers = new List<long> { 10, 20, 30 } });
        Assert.Equal(20L, result);
    }

    [Fact]
    public void GenericListMembership()
    {
        var result = Eval("20 in Numbers", new ListContext { Numbers = new List<long> { 10, 20, 30 } });
        Assert.Equal(true, result);
    }

    [Fact]
    public void NonGenericListIndex()
    {
        var result = Eval("LegacyNumbers[2]", new ListContext { LegacyNumbers = new ArrayList { 3L, 4L, 5L } });
        Assert.Equal(5L, result);
    }

    [Fact]
    public void NonGenericListMembership()
    {
        var result = Eval("4 in LegacyNumbers", new ListContext { LegacyNumbers = new ArrayList { 3L, 4L, 5L } });
        Assert.Equal(true, result);
    }

    [Fact]
    public void MapIndex()
    {
        var result = Eval("Values[\"b\"]", new ListContext
        {
            Values = new Dictionary<string, long>
            {
                ["a"] = 1,
                ["b"] = 2,
            }
        });

        Assert.Equal(2L, result);
    }

    [Fact]
    public void MapMembershipChecksKeys()
    {
        var result = Eval("\"b\" in Values", new ListContext
        {
            Values = new Dictionary<string, long>
            {
                ["a"] = 1,
                ["b"] = 2,
            }
        });

        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonElementScalarMembershipMatchesTypedDictionaryKeys()
    {
        using var document = JsonDocument.Parse("""{"field":"field"}""");
        var result = Eval("""field in {'field': 10}""", document.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void MissingMapMembershipReturnsFalse()
    {
        var result = Eval("\"missing\" in Values", new ListContext());
        Assert.Equal(false, result);
    }

    [Fact]
    public void MixedKeyTypeMapMembershipUsesCanonicalObjectMapShape()
    {
        var result = Eval("1u in {1: \"int\", 1u: \"uint\"}");
        Assert.Equal(true, result);
    }

    [Fact]
    public void ListIndexOutOfBoundsThrowsRuntimeError()
    {
        var ex = Assert.Throws<CelRuntimeException>(() => Eval("[1, 2][2]"));
        Assert.Equal("index_out_of_bounds", ex.ErrorCode);
    }

    [Fact]
    public void MissingMapKeyThrowsRuntimeError()
    {
        var ex = Assert.Throws<CelRuntimeException>(() => Eval("Values[\"missing\"]", new ListContext()));
        Assert.Equal("no_such_field", ex.ErrorCode);
    }

    [Fact]
    public void JsonElementArrayIndex()
    {
        using var document = JsonDocument.Parse("""{"items":[7,8,9]}""");
        var result = Eval("items[1]", document.RootElement);
        Assert.IsType<JsonElement>(result);
        Assert.Equal(8L, ((JsonElement)result!).GetInt64());
    }

    [Fact]
    public void JsonElementObjectPropertyIndex()
    {
        using var document = JsonDocument.Parse("""{"obj":{"name":"cel"}}""");
        var result = Eval("obj[\"name\"]", document.RootElement);
        Assert.IsType<JsonElement>(result);
        Assert.Equal("cel", ((JsonElement)result!).GetString());
    }

    [Fact]
    public void JsonElementObjectMembershipChecksKeys()
    {
        using var document = JsonDocument.Parse("""{"obj":{"name":"cel"}}""");
        var result = Eval("""'name' in obj""", document.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonElementArrayMembershipUsesCelEquality()
    {
        using var document = JsonDocument.Parse("""{"items":[1,2,3]}""");
        var result = Eval("2u in items", document.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonNodeObjectMembershipChecksKeys()
    {
        var node = JsonNode.Parse("""{"obj":{"name":"cel"}}""")!;
        var result = Eval("""'name' in obj""", node);
        Assert.Equal(true, result);
    }

    [Fact]
    public void JsonNodeArrayMembershipUsesCelEquality()
    {
        var node = JsonNode.Parse("""{"items":[1,2,3]}""")!;
        var result = Eval("2u in items", node);
        Assert.Equal(true, result);
    }

    [Fact]
    public void InOperatorRejectsNonContainerRightOperand()
    {
        Assert.Throws<CelCompilationException>(() => Eval("1 in 2"));
    }

    [Fact]
    public void TypedListConcatenationReturnsTypedArray()
    {
        var result = Eval("[1, 2] + [3, 4]");
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, list);
    }

    [Fact]
    public void MixedListConcatenationReturnsObjectArray()
    {
        var result = Eval("[1, 'two'] + [3u, null]");
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Equal(4, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal("two", list[1]);
        Assert.Equal(3UL, list[2]);
        Assert.Null(list[3]);
    }

    [Fact]
    public void ContextListConcatenationUsesArrayBackedResult()
    {
        var result = Eval("Numbers + [40]", new ListContext { Numbers = new List<long> { 10, 20, 30 } });
        var list = Assert.IsAssignableFrom<IList<long>>(result);
        Assert.Equal(new long[] { 10, 20, 30, 40 }, list);
    }

    [Fact]
    public void NonGenericListConcatenationUsesObjectArrayResult()
    {
        var result = Eval("LegacyNumbers + [6]", new ListContext { LegacyNumbers = new ArrayList { 3L, 4L, 5L } });
        var list = Assert.IsAssignableFrom<IList<object?>>(result);
        Assert.Equal(new object?[] { 3L, 4L, 5L, 6L }, list);
    }
}
