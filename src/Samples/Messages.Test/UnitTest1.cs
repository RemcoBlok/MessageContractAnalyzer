using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Messages.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var command = CommandActivator.Create<ICreateCommand>(new
            {
                StreamId = Guid.NewGuid(),
                Name = "Name1",
                BillingAddress = new
                {
                    Street = "street1",
                    Place = "place1"
                },
                DeliveryAddress = new
                {
                    Street = "street2",
                    Place = "place2"
                },
                Identifications = new[]
                {
                    new
                    {
                        Type = "Paspoort",
                        IssuingCountry = "NL",
                        Number = "Paspoort1"
                    },
                    new
                    {
                        Type = "Rijbewijs",
                        IssuingCountry = "NL",
                        Number = "Rijbewijs1"
                    }
                },
                Documents = new[]
                {
                    new
                    {
                        Type = "BSN",
                        IssuingCountry = "NL",
                        Number = "BSN1"
                    },
                    new
                    {
                        Type = "Buitenlands ID",
                        IssuingCountry = "GB",
                        Number = "NI1"
                    }
                },
            });

            Assert.IsFalse(command.CommandId == Guid.Empty);
        }

        [TestMethod]
        public void TestMethod2()
        {
            var billingAddress = ModelActivator.Create<IAddress>(new
            {
                Street = string.Empty,
                Place = string.Empty
            });

            var identifications = ModelActivator.Create<List<IIdentification>>(new[]
            {
                new
                {
                    Type = string.Empty,
                    IssuingCountry = string.Empty,
                    Number = string.Empty
                },
                new
                {
                    Type = string.Empty,
                    IssuingCountry = string.Empty,
                    Number = string.Empty
                }
            });

            var command = CommandActivator.Create<ICreateCommand>(new
            {
                StreamId = Guid.Empty,
                Name = string.Empty,
                BillingAddress = billingAddress,
                DeliveryAddress = new
                {
                    Street = string.Empty,
                    Place = string.Empty
                },
                Identifications = identifications,
                Documents = new[]
                {
                    new
                    {
                        Type = string.Empty,
                        IssuingCountry = string.Empty,
                        Number = string.Empty
                    },
                    new
                    {
                        Type = string.Empty,
                        IssuingCountry = string.Empty,
                        Number = string.Empty
                    }
                }
            });

            Assert.IsFalse(command.CommandId == Guid.Empty);
        }

        [TestMethod]
        public void TestMethod3()
        {
            var request = new CreateRequest();

            var command = CommandActivator.Create<ICreateCommand>(new
            {
                request.StreamId,
                request.Name,
                BillingAddress = new
                {
                    request.BillingAddress.Place,
                    request.BillingAddress.Street
                },
                DeliveryAddress = new
                {
                    request.DeliveryAddress.Place,
                    request.DeliveryAddress.Street
                },
                Identifications = request.Identifications.Select(i => new
                {
                    i.Type,
                    i.IssuingCountry,
                    i.Number
                }).ToList(),
                Documents = request.Documents.Select(d => new
                {
                    d.Type,
                    d.IssuingCountry,
                    d.Number
                }).ToList()
            });

            Assert.IsFalse(command.CommandId == Guid.Empty);
        }
    }
}
