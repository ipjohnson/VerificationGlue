using Glue.Mocks;
using Vendor;

var gateway = Substitute.For<IGateway>();
gateway.Send("a").Returns("b");
Console.WriteLine(gateway.Send("a"));
