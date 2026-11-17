// case: mocks-shape-0006
// layer: shape
// origin: mocks.md, internal types and nested generics
// expect: pass

namespace Shapes.Visibility
{
    internal interface IInternal
    {
        internal int Hidden();
        int Visible();
    }

    public class Outer<T>
    {
        public interface IInner<U>
        {
            T Convert(U value);
        }

        internal interface IInternalInner
        {
            T Value { get; }
        }

        public class Middle
        {
            public interface IDeep
            {
                T Deep();
            }
        }
    }

    public interface INested<T>
    {
        Outer<T>.IInner<int> Inner();
    }

    public class Order
    {
    }

    public interface IRepository<T>
    {
        T? Find(int id);
    }

    public class MockTargets
    {
        internal IInternal? Internal;
        public Outer<string>.IInner<int>? ClosedInner;
        internal Outer<int>.IInternalInner? ClosedInternalInner;
        public Outer<long>.Middle.IDeep? ClosedDeep;
        public IRepository<Order>? Repository;
        public IRepository<Outer<Order>.IInner<Order>>? NestedArgument;
    }
}

public interface IGlobalNamespace
{
    Shapes.Visibility.Order Get();
}
