namespace Vendor;

public class Payment
{
}

public interface IGateway
{
    Payment Charge(Payment payment);
}

public interface IGateway<T>
{
    T Charge(T payment, Payment raw);
}
