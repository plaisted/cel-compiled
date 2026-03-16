using System;
using Cel.Compiled.Compiler;
using Xunit;

namespace Cel.Compiled.Tests;

public class CelResultTests
{
    [Fact]
    public void SuccessResult_HasValue()
    {
        var result = CelResult<long>.Of(42L);
        Assert.False(result.IsError);
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void ErrorResult_HasError()
    {
        var error = new CelError("test_error", "something went wrong");
        var result = CelResult<long>.FromError(error);
        Assert.True(result.IsError);
        Assert.Equal("test_error", result.Error.ErrorCode);
        Assert.Equal("something went wrong", result.Error.Message);
    }

    [Fact]
    public void GetValueOrThrow_ReturnsValueOnSuccess()
    {
        var result = CelResult<string>.Of("hello");
        Assert.Equal("hello", result.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_ThrowsOnError()
    {
        var error = CelError.DivisionByZero();
        var result = CelResult<long>.FromError(error);
        var ex = Assert.Throws<CelRuntimeException>(() => result.GetValueOrThrow());
        Assert.Equal("division_by_zero", ex.ErrorCode);
    }

    [Fact]
    public void SuccessResult_Bool_False()
    {
        // Important for &&/|| absorption: CelResult<bool> with value=false must work
        var result = CelResult<bool>.Of(false);
        Assert.False(result.IsError);
        Assert.False(result.Value);
    }

    [Fact]
    public void SuccessResult_Bool_True()
    {
        var result = CelResult<bool>.Of(true);
        Assert.False(result.IsError);
        Assert.True(result.Value);
    }

    [Fact]
    public void ErrorResult_Bool_IsError()
    {
        var error = CelError.NoMatchingOverload("_/_", typeof(long), typeof(long));
        var result = CelResult<bool>.FromError(error);
        Assert.True(result.IsError);
    }

    [Fact]
    public void DefaultStruct_IsNotError()
    {
        // Default-initialized struct should behave as success with default(T)
        CelResult<long> result = default;
        Assert.False(result.IsError);
        Assert.Equal(0L, result.Value);
    }

    [Fact]
    public void CelError_FactoryMethods()
    {
        var noOverload = CelError.NoMatchingOverload("_+_", typeof(long), typeof(ulong));
        Assert.Equal("no_matching_overload", noOverload.ErrorCode);
        Assert.Contains("_+_", noOverload.Message);

        var divZero = CelError.DivisionByZero();
        Assert.Equal("division_by_zero", divZero.ErrorCode);

        var overflow = CelError.Overflow("_*_");
        Assert.Equal("overflow", overflow.ErrorCode);
        Assert.Contains("_*_", overflow.Message);

        var modZero = CelError.ModuloByZero();
        Assert.Equal("modulo_by_zero", modZero.ErrorCode);
    }

    [Fact]
    public void CelError_ToException()
    {
        var error = CelError.Overflow("_+_");
        var ex = error.ToException();
        Assert.IsType<CelRuntimeException>(ex);
        Assert.Equal("overflow", ex.ErrorCode);
        Assert.Contains("_+_", ex.Message);
    }

    [Fact]
    public void SuccessResult_NullReferenceType()
    {
        // CelResult<object> with null value is a success, not an error
        var result = CelResult<object?>.Of(null);
        Assert.False(result.IsError);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ExpressionTreeIntegration_CanUseAsVariable()
    {
        // Verify CelResult<T> works with Expression.Variable — this is the key integration point
        var resultType = typeof(CelResult<bool>);
        var variable = System.Linq.Expressions.Expression.Variable(resultType, "result");
        Assert.Equal(resultType, variable.Type);

        // Can access IsError property
        var isError = System.Linq.Expressions.Expression.Property(variable, nameof(CelResult<bool>.IsError));
        Assert.Equal(typeof(bool), isError.Type);

        // Can access Value property
        var value = System.Linq.Expressions.Expression.Property(variable, nameof(CelResult<bool>.Value));
        Assert.Equal(typeof(bool), value.Type);

        // Can call static Of method
        var ofMethod = resultType.GetMethod(nameof(CelResult<bool>.Of))!;
        var ofCall = System.Linq.Expressions.Expression.Call(ofMethod, System.Linq.Expressions.Expression.Constant(true));
        Assert.Equal(resultType, ofCall.Type);

        // Can call GetValueOrThrow
        var getMethod = resultType.GetMethod(nameof(CelResult<bool>.GetValueOrThrow))!;
        var getCall = System.Linq.Expressions.Expression.Call(variable, getMethod);
        Assert.Equal(typeof(bool), getCall.Type);
    }
}
