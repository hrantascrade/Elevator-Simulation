using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Elevator
{
    public struct CallOnFloor
    {
        public bool up;
        public bool down;
    }
    class Passenger
    {
        public int from;
        public int to;

        public Passenger(int from, int to)
        {
            this.from = from;
            this.to = to;
        }
    }
    enum Direction
    {
        NONE,
        UP,
        DOWN,
    }
    class Building
    {
        public int max_floors;
        public ConcurrentDictionary<Passenger, int>[] people_on_floor;
        public Elevator elevator;

        public void Check(int value)
        {
            if (value < 0 || value >= max_floors)
                throw new ArgumentException("Your value must be whithin 0 and max_floor - 1.");
        }
        public Building(int max_floors)
        {
            if (max_floors < 0)
                throw new ArgumentException("Maximum floor number must be a positive integer.");

            this.max_floors = max_floors;
            people_on_floor = new ConcurrentDictionary<Passenger, int>[max_floors];
            for (int i = 0; i < max_floors; ++i)
                people_on_floor[i] = new ConcurrentDictionary<Passenger, int>();
        }

        public Building(ConcurrentDictionary<Passenger, int>[] people_on_floor)
        {
            if (people_on_floor.Length == 0)
                throw new ArgumentException("The building must have at least 1 floor");

            this.people_on_floor = people_on_floor;
            this.max_floors = people_on_floor.Length;
        }

        public void AddPassenger(int floor, Passenger p)
        {
            Check(floor);

            people_on_floor[floor].TryAdd(p, 0);

            if (p.from > p.to)
                elevator.CallFrom(floor, Direction.DOWN);
            else if (p.from < p.to)
                elevator.CallFrom(floor, Direction.UP);

            //Let's assume that people who are confused are pressing the down button
            else
                elevator.CallFrom(floor, Direction.DOWN);
        }

        public void checkForPeopleLeft()
        {
            while (true)
            {
                // Check if there are people who want to repress the buttons
                for (int i = 0; i < max_floors; ++i)
                {
                    List < Passenger > pof = people_on_floor[i].Keys.ToList();
                    for (int j = pof.Count - 1; j >= 0; --j)
                    {
                        Passenger p = pof[j];
                        if (elevator != null)
                        {
                            if (p.from > p.to) elevator.CallFrom(i, Direction.DOWN);
                            else if (p.from < p.to) elevator.CallFrom(i, Direction.UP);
                            else elevator.CallFrom(i, Direction.DOWN);
                        }
                    }
                }
            }
        }
    }
    class Elevator
    {
        CallOnFloor[] callOnFloor;
        Building building;
        int capacity;
        List<Passenger> passengers;
        int pos;
        bool[] pressed_buttons;
        Direction direction;

        public Elevator(Building building, int capacity)
        {
            this.building = building;
            this.capacity = capacity;

            pressed_buttons = new bool[building.max_floors];
            callOnFloor = new CallOnFloor[building.max_floors];
            pos = 0;
            passengers = new List<Passenger>();
            direction = Direction.NONE;
        }

        void Print(int value, int floor, bool entered)
        {
            if (value != 0)
            {
                string passengerString = value == 1 ? "passenger" : "passengers";
                string action = entered ? "entered" : "left";
                Console.WriteLine("{0} {1} {2} on the {3} floor", value, passengerString, action, floor + 1);
            }
        }
        public void CallFrom(int floor, Direction dir)
        {
            building.Check(floor);
            if (dir == Direction.NONE) throw new ArgumentException("Invalid Direction");

            if(dir == Direction.UP) callOnFloor[floor].up = true;
            else if (dir == Direction.DOWN) callOnFloor[floor].down = true;
        }

        void PressButton(int button)
        {
            building.Check(button);

            pressed_buttons[button] = true;
        }

        void unselectButton(int floor, Direction dir)
        {
            if (dir == Direction.UP)
                callOnFloor[floor].up = false;
            else if (dir == Direction.DOWN)
                callOnFloor[floor].up = false;
            else
            {
                callOnFloor[floor].up = false;
                callOnFloor[floor].down = false;
            }
        }

        void StopAtFloor(int floor, Direction dir)
        {
            //Go to floor, unselect the buttons and remove all passengers who were going to this floor
            unselectButton(floor, dir);
            pos = floor;
            pressed_buttons[pos] = false;
            int removed = passengers.RemoveAll(passenger => passenger.to == pos);

            Print(removed, floor, false);

            //Analize all passengers on the floor
            int entered = 0;
            foreach (var p in building.people_on_floor[pos].ToList())
            {
                if (passengers.Count < capacity)
                {
                    //Add the passenger based on elevators direction
                    if ((dir == Direction.UP && p.Key.to > pos) || (dir == Direction.DOWN && p.Key.to < pos) || dir == Direction.NONE)
                    {
                        //Remove the passenger from the floor and unselect the button
                        int tempValue;
                        building.people_on_floor[pos].TryRemove(p.Key, out tempValue);
                        unselectButton(floor, dir);

                        //Add the passenger to the elevator and press his button
                        passengers.Add(p.Key);
                        PressButton(p.Key.to);

                        entered++;
                    }
                }
                else break;
            }

            Print(entered, floor, true);
        }

        bool floorCall(int index)
        {
            building.Check(index);

            return callOnFloor[index].up || callOnFloor[index].down;
        }

        void closestTo(int pos)
        {
            // Find the closest button that has been pressed
            if(pressed_buttons[pos])
            {
                StopAtFloor(pos, Direction.NONE);
                return;
            }
            int upIndex = pos, downIndex = pos;
            while (downIndex != 0 || upIndex != building.max_floors - 1)
            {
                if (downIndex > 0) downIndex--;
                if (upIndex < building.max_floors - 1) upIndex++;

                if (pressed_buttons[downIndex])
                {
                    direction = Direction.DOWN;
                    return;
                }
                if (pressed_buttons[upIndex])
                {
                    direction = Direction.UP;
                    return;
                }
            }

            // Find the closest floor from which the elevator was called
            if (floorCall(pos))
            {
                StopAtFloor(pos, Direction.NONE);
                return;
            }
            upIndex = pos; downIndex = pos;
            int index = building.max_floors;
            while (downIndex != 0 || upIndex != building.max_floors - 1)
            {
                if (downIndex > 0) downIndex--;
                if (upIndex < building.max_floors - 1) upIndex++;

                if (floorCall(downIndex))
                {
                    StopAtFloor(downIndex, Direction.NONE);
                    return;
                }
                else if (floorCall(upIndex))
                {
                    StopAtFloor(upIndex, Direction.NONE);
                    return;
                }
            }
        }

        public void StartWorking()
        {
            Console.WriteLine("The elevator is working...");

            while (true)
            {
                //The elevator is going up at this moment.
                if (direction == Direction.UP)
                {
                    for (int i = pos + 1; i < building.max_floors; ++i)
                    {
                        if (callOnFloor[i].up == true || pressed_buttons[i] == true)
                        {
                            StopAtFloor(i, direction);
                        }
                    }

                    //Set the direction to NONE
                    direction = Direction.NONE;
                }

                //The elevator is going down at this moment.
                else if (direction == Direction.DOWN)
                {
                    for (int i = pos - 1; i >= 0; --i)
                    {
                        if (callOnFloor[i].down == true || pressed_buttons[i] == true)
                        {
                            StopAtFloor(i, direction);
                        }
                    }

                    //Set the direction to NONE
                    direction = Direction.NONE;
                }

                //The elevator is not moving.
                else if (direction == Direction.NONE)
                {
                    // Find the closest floor.
                    closestTo(pos);
                }
            }
        }
    }



    class Program
    {
        const int MAX_FLOORS = 5;
        const int MAX_CAPACITY = 5;
        const int NUM_OF_PASSENGERS = 10;
        static void Main(string[] args)
        {
            Building building = new Building(MAX_FLOORS);

            Elevator elevator = new Elevator(building, MAX_CAPACITY);
            building.elevator = elevator;

            Task sw = new Task(() => elevator.StartWorking());
            Task cpl = new Task(() => building.checkForPeopleLeft());
            sw.Start();
            cpl.Start();

            //Generate random passengers
            Random random = new Random();
            for (int i = 0; i < NUM_OF_PASSENGERS; ++i)
            {
                int from = random.Next(MAX_FLOORS);
                int to = random.Next(MAX_FLOORS);
                //while (from == to)
                //    to = random.Next(MAX_FLOORS);
                building.AddPassenger(from, new Passenger(from, to));
            }

            //int from = 0;
            //int to = 0;
            //building.AddPassenger(from, new Passenger(from, to));

            //from = 0;
            //to = 3;
            //building.AddPassenger(from, new Passenger(from, to));

            //from = 0;
            //to = 0;
            //building.AddPassenger(from, new Passenger(from, to));

            //from = 2;
            //to = 0;
            //building.AddPassenger(from, new Passenger(from, to));

            //from = 0;
            //to = 0;
            //building.AddPassenger(from, new Passenger(from, to));

            //from = 3;
            //to = 4;
            //building.AddPassenger(from, new Passenger(from, to));

            Task.WaitAll(cpl);
        }
    }
}