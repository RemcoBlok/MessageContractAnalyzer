using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Messages
{
    public interface ICommand
    {
        [ActivatorInitialized]
        Guid CommandId { get; }
        Guid StreamId { get; }
    }

    public interface IAddress
    {
        string Street { get; }
        string Place { get; }
    }

    public interface IIdentification
    {
        string Type { get; }
        string IssuingCountry { get; }
        string Number { get; }
    }

    public interface ICreateCommand : ICommand
    {
        string Name { get; }
        IAddress BillingAddress { get; }
        IAddress DeliveryAddress { get; }
        IReadOnlyList<IIdentification> Identifications { get; }
        IReadOnlyList<IIdentification> Documents { get; }
    }
}

namespace Messages
{
    using System.Linq;

    public class AddressModel
    {
        public string Street { get; }
        public string Place { get; }
    }

    public class IdentificationModel
    {
        public string Type { get; }
        public string IssuingCountry { get; }
        public string Number { get; }
    }

    public class CreateRequest
    {
        public Guid StreamId { get; }
        public string Name { get; }
        public AddressModel BillingAddress { get; } = new AddressModel();
        public AddressModel DeliveryAddress { get; } = new AddressModel();
        public IReadOnlyList<IdentificationModel> Identifications { get; } = new List<IdentificationModel>();
        public IReadOnlyList<IdentificationModel> Documents { get; } = new List<IdentificationModel>();
    }

    class Program
    {
        private static void Main()
        {
            var billingAddress = ModelActivator.Create<IAddress>(new
            {
                Street = string.Empty,
                Place = string.Empty
            });

            var identifications = ModelActivator.Create<IReadOnlyList<IIdentification>>(new[]
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

            var request = new CreateRequest();

            var command2 = CommandActivator.Create<ICreateCommand>(new
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
        }
    }
}
