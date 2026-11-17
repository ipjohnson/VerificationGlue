# CSharpAuthor issue drafts

This page holds the text of the issue filed for each change in [Required changes to CSharpAuthor](north-star.md#required-changes-to-csharpauthor). The status column links each filed issue in [ipjohnson/CSharpAuthor](https://github.com/ipjohnson/CSharpAuthor/issues).

| Change | Title | Status |
|---|---|---|
| 1 | Add OutputContext.Reset() | [ipjohnson/CSharpAuthor#27](https://github.com/ipjohnson/CSharpAuthor/issues/27) |
| 2 | Cache TypeDefinition.Get(Type) results by Type | [ipjohnson/CSharpAuthor#28](https://github.com/ipjohnson/CSharpAuthor/issues/28) |
| 3 | Add value equality to EmitProfile | [ipjohnson/CSharpAuthor#29](https://github.com/ipjohnson/CSharpAuthor/issues/29) |
| 4 | Add an equatable snapshot of AttributeInstance | [ipjohnson/CSharpAuthor#30](https://github.com/ipjohnson/CSharpAuthor/issues/30) |

## 1. Add OutputContext.Reset()

```markdown
## What

Add `OutputContext.Reset()`. It clears the output and the derived type references and keeps the `StringBuilder` buffer.

## Why

A source generator can then reuse one `OutputContext` for many small files, such as interceptors. Today `OutputContext` has no reset method, so a generator creates one context per file.

## Workaround in VerificationGlue

One `CSharpFileDefinition` and one `OutputContext` per generated file.

This is change 1 of the four listed in [VerificationGlue north star: Required changes to CSharpAuthor](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/north-star.md#required-changes-to-csharpauthor). The VerificationGlue generators run on CSharpAuthor 2.0.0 with the workaround until this lands, so it does not block them.
```

## 2. Cache TypeDefinition.Get(Type) results by Type

```markdown
## What

Cache the definitions returned by `TypeDefinition.Get(Type)`, keyed by `Type`. The cache must be thread-safe, because Roslyn runs generators in parallel.

## Why

`TypeDefinition.Get(Type)` allocates a new definition on every call except for built-in types. Generators call it often for BCL types such as `Task<T>` and `CancellationToken`.

## Workaround in VerificationGlue

Each generator keeps its own `static readonly` cache of the BCL types it uses. That cache goes away once this lands. The `KnownTypes` classes for VerificationGlue's own runtime types stay, because those types are named by string.

This is change 2 of the four listed in [VerificationGlue north star: Required changes to CSharpAuthor](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/north-star.md#required-changes-to-csharpauthor). The VerificationGlue generators run on CSharpAuthor 2.0.0 with the workaround until this lands, so it does not block them.
```

## 3. Add value equality to EmitProfile

```markdown
## What

Give `EmitProfile` value equality over every setting that changes output.

## Why

An incremental generator reruns a stage only when its input model compares unequal. `EmitProfile` has no value equality, so it cannot be part of a pipeline model. With value equality, a frozen profile can flow through the pipeline.

## Workaround in VerificationGlue

The pipeline carries an equatable `EmitSettings` record holding the language version and the editorconfig values the profile reads. The output step turns that record into a frozen profile through a small cache keyed by the record.

This is change 3 of the four listed in [VerificationGlue north star: Required changes to CSharpAuthor](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/north-star.md#required-changes-to-csharpauthor). The VerificationGlue generators run on CSharpAuthor 2.0.0 with the workaround until this lands, so it does not block them.
```

## 4. Add an equatable snapshot of AttributeInstance

```markdown
## What

Add an equatable snapshot of `AttributeInstance` from the Roslyn bridge. It holds the attribute type as `ITypeDefinition` and its arguments as values.

## Why

`AttributeInstance` is a sealed class with no value equality, so it cannot be part of an incremental pipeline model. With an equatable snapshot, attribute reads can flow through the pipeline.

## Workaround in VerificationGlue

Generators read `AttributeInstance` in the transform and copy what the emitter needs into their own records. The `VerificationGlue.Tests` generator design names this change.

This is change 4 of the four listed in [VerificationGlue north star: Required changes to CSharpAuthor](https://github.com/ipjohnson/VerificationGlue/blob/main/docs/north-star.md#required-changes-to-csharpauthor). The VerificationGlue generators run on CSharpAuthor 2.0.0 with the workaround until this lands, so it does not block them.
```
