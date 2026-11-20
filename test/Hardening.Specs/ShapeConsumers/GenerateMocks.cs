extern alias V1;
extern alias V2;

using VerificationGlue.Mocks;

// The types Fakes.Fresh<T>() creates. Shapes.Obsolete.IOld is not here, because its [Obsolete] is an
// error and an assembly attribute cannot name it. Its consumer relies on its Mock.Of<T>() call instead.
// The members of these types that cannot be mocked report VGM0003 here, which SHAPE-007 expects.
#pragma warning disable VGM0003
#pragma warning disable CS0618
[assembly: GenerateMock(typeof(Shapes.Collisions.IReadWriter))]
[assembly: GenerateMock(typeof(Shapes.Collisions.IDiamond))]
[assembly: GenerateMock(typeof(Shapes.Collisions.IDictionaryLike<string, int>))]
[assembly: GenerateMock(typeof(Shapes.Constraints.IConstrained<string, int, System.Guid, int, object>))]
[assembly: GenerateMock(typeof(Shapes.Constraints.IGenericMethods))]
[assembly: GenerateMock(typeof(Shapes.Constraints.IOpen<System.IO.MemoryStream>))]
[assembly: GenerateMock(typeof(Shapes.Constraints.IOpenAllowsRefStruct<int>))]
[assembly: GenerateMock(typeof(Shapes.Members.IAsync))]
[assembly: GenerateMock(typeof(Shapes.Members.IRefReturns))]
[assembly: GenerateMock(typeof(Shapes.Members.IEvents))]
[assembly: GenerateMock(typeof(Shapes.Members.IDefaults))]
[assembly: GenerateMock(typeof(Shapes.Members.IIndexers))]
[assembly: GenerateMock(typeof(Shapes.Names.@interface))]
[assembly: GenerateMock(typeof(Shapes.Names.IContextual))]
[assembly: GenerateMock(typeof(Shapes.Names.IAccessors))]
[assembly: GenerateMock(typeof(Shapes.Names.IHiding))]
[assembly: GenerateMock(typeof(Shapes.Nullable.INullable))]
[assembly: GenerateMock(typeof(Shapes.Nullable.ITuples))]
[assembly: GenerateMock(typeof(Shapes.Nullable.IGeneric<string>))]
[assembly: GenerateMock(typeof(Shapes.Nullable.IGeneric<int>))]
[assembly: GenerateMock(typeof(Shapes.Obsolete.IUsesRetired))]
[assembly: GenerateMock(typeof(Shapes.Obsolete.IObsoleteMember))]
[assembly: GenerateMock(typeof(Shapes.Parameters.IParameters))]

// The test registration cannot name an extern-aliased type, so it leaves these attributes out of
// TestContext.Attributes. The mock generator still reads them.
#pragma warning disable VGT0006
[assembly: GenerateMock(typeof(V1::Vendor.IGateway))]
[assembly: GenerateMock(typeof(V2::Vendor.IGateway))]
[assembly: GenerateMock(typeof(V1::Vendor.IGateway<V2::Vendor.Payment>))]
[assembly: GenerateMock(typeof(V1::Vendor.IGateway<Shapes.ExternAlias.Local>))]
[assembly: GenerateMock(typeof(System.Collections.Generic.IList<V2::Vendor.Payment>))]
[assembly: GenerateMock(typeof(V1::Vendor.Ledger<Shapes.ExternAliasNested.Local>.IEntry))]
#pragma warning restore VGT0006
[assembly: GenerateMock(typeof(Friend.IFriendOnly))]
[assembly: GenerateMock(typeof(Friend.IPartlyInternal))]
[assembly: GenerateMock(typeof(Friend.IInternalDefault))]
[assembly: GenerateMock(typeof(Shapes.RefStruct.ISpans))]
[assembly: GenerateMock(typeof(Shapes.Pointers.IPointers))]
#pragma warning restore CS0618
#pragma warning restore VGM0003
