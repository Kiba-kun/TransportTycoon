using System;
using System.Collections.Generic;
using System.Linq;

namespace DDD.Dojo.Tycoon.Client
{
    public class Transport
    {
        public event Action<Transport> HasFinishedDelivery;

        protected Way way;
        protected Container container;
        private int hoursInTheWay = 0;

        public Transport(Way way, Container container)
        {
            this.Setup(way, container);
        }

        public void Setup(Way way, Container container)
        {
            this.hoursInTheWay = 0;
            this.way = way;
            this.container = container;
        }

        public virtual void Tick()
        {
            if(this.way == null)
            {
                return;
            }

            this.hoursInTheWay++;

            if (ArrivedBackToSource())
            {
                this.HasFinishedDelivery(this);
                return;
            }

            if (ArrivedToDestination())
            {
                this.UnloadCargo(this.way.Destinaiton);
                this.Setup(this.way.GetWayBack(), (Container)null);
                return;
            }
        }

        private bool ArrivedBackToSource() => this.hoursInTheWay == this.way.HoursToComplete && this.container == null;

        private bool ArrivedToDestination() => this.hoursInTheWay == this.way.HoursToComplete;

        private void UnloadCargo(GeographicPoint destinaiton)
        {
            destinaiton.AcceptContainer(this.container);
            this.container = null;
        }
    }

    public class Ship : Transport
    {
        public Ship() : base(null, null)
        {
        }

        public bool IsFree() => this.container == null && this.way == null;
    }

    public class Factory : GeographicPoint
    {
        public Factory(
            Container[] containers)
            : base(containers)
        {
        }
    }

    public class Container { }

    public class GeographicPoint
    {
        private readonly Queue<Container> containers;

        public GeographicPoint()
            : this(Array.Empty<Container>())
        {
        }

        protected GeographicPoint(
            IEnumerable<Container> containers)
        {
            this.containers = new Queue<Container>(containers);
        }

        public IReadOnlyCollection<Container> Containers => this.containers;

        public virtual void AcceptContainer(Container container) => this.containers.Enqueue(container);

        public Container UnloadContainer() => this.containers.Dequeue();
    }

    public class Port : GeographicPoint
    {
        public event Action<Port> NewContainerArrived;

        public override void AcceptContainer(Container container)
        {
            base.AcceptContainer(container);
            this.NewContainerArrived(this);
        }
    }

    public class Way
    {
        public Way(
            GeographicPoint source,
            GeographicPoint destinaiton,
            byte hoursToComplete)
        {
            Source = source;
            Destinaiton = destinaiton;
            HoursToComplete = hoursToComplete;
        }

        public GeographicPoint Source { get; }
        public GeographicPoint Destinaiton { get; }
        public byte HoursToComplete { get; }

        internal Way GetWayBack() => new Way(this.Destinaiton, this.Source, this.HoursToComplete);

        public static implicit operator byte(Way way) => way.HoursToComplete;
    }

    internal class Map
    {
        public int Calculate(IEnumerable<string> destinationsPoints)
        {
            var containersToDeliver = destinationsPoints.Select(_ => new Container()).ToArray();

            var a = new GeographicPoint();
            var b = new GeographicPoint();
            var port = new Port();
            var factory = new Factory(containersToDeliver);

            var factoryToB = new Way(factory, b, 5);
            var portToA = new Way(port, a, 4);
            var factoryToPort = new Way(factory, port, 1);

            var expectedACargos = destinationsPoints.Count(x => x == "A");
            var expectedBCargos = destinationsPoints.Count(x => x == "B");

            var destinationQueue = new Queue<Way>(
                destinationsPoints.Select(x =>
                x switch
                {
                    "A" => factoryToPort,
                    "B" => factoryToB,
                    _ => throw new InvalidOperationException("Neither A, nor B, dude, are you OK?")
                })
            );

            var trucks = new List<Transport>
            {
                new Transport(destinationQueue.Dequeue(), factory.UnloadContainer()),
                new Transport(destinationQueue.Dequeue(), factory.UnloadContainer()),
            };
            var ships = new List<Ship>
            { 
                new Ship()
            };

            trucks.ForEach(x =>
            {
                x.HasFinishedDelivery += (truck) =>
                {
                    if (factory.Containers.Count == 0)
                    {
                        truck.Setup(null, null);
                        return;
                    }

                    truck.Setup(destinationQueue.Dequeue(), factory.UnloadContainer());
                };
            });

            ships.ForEach(x =>
            {
                x.HasFinishedDelivery += (ship) =>
                {
                    if (port.Containers.Count == 0)
                    {
                        ship.Setup(null, null);
                        return;
                    }

                    ship.Setup(portToA, port.UnloadContainer());
                };
            });

            port.NewContainerArrived += (port) =>
            {
                var freeShips = ships.Where(x => x.IsFree()).ToList();
                freeShips.ForEach(freeShip =>
                {
                    freeShip.Setup(portToA, port.UnloadContainer());
                });
            };

            var timeCounter = 0;
            while (
                a.Containers.Count != expectedACargos ||
                b.Containers.Count != expectedBCargos)
            {
                trucks.ForEach(x => x.Tick());
                ships.ForEach(x => x.Tick());

                timeCounter++;
            }

            return timeCounter;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var destinationPointLists = Console.ReadLine().ToArray().Select(x => x.ToString().Trim().ToUpper());
            var facade = new Map();
            var result = facade.Calculate(destinationPointLists);

            Console.WriteLine($"Result: {result}");
            Console.ReadKey();
        }
    }
}
