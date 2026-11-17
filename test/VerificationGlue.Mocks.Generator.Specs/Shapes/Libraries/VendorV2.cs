namespace Vendor;

public class Payment
{
}

public interface IGateway
{
    Payment Charge(Payment payment, int retries);
}
