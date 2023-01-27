using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EntitasTest
{

    /// <summary>
    /// A* pathfinding algorithm implementation.
    /// </summary>
    /// <typeparam name="T">State type.</typeparam>
    public class AStar<T>
    {
        private T InvalidState;
        private Func<T, T[]> GetTransitions;
        private Func<T, T, float> GetEdgeWeight;
        private Func<T, T, float> Heuristic;
        private Func<T, T, bool> IsGoalFunc;

        public AStar(
            T invalidState,
            Func<T, T[]> GetTransitions, 
            Func<T, T, float> GetEdgeWeight, 
            Func<T, T, float> Heuristic
        )
            : this(
                  invalidState, 
                  GetTransitions,
                  GetEdgeWeight, 
                  Heuristic, 
                  (a, b) => EqualityComparer<T>.Default.Equals(a, b)
              )
        {
        }

        public AStar(
            T invalidState,
            Func<T, T[]> GetTransitions,
            Func<T, T, float> GetEdgeWeight,
            Func<T, T, float> Heuristic,
            Func<T, T, bool> IsGoalFunc
        )
        {
            InvalidState = invalidState;
            this.GetTransitions = GetTransitions;
            this.GetEdgeWeight = GetEdgeWeight;
            this.Heuristic = Heuristic;
            this.IsGoalFunc = IsGoalFunc;
        }

        private IEnumerable<T> ReconstructPath(T Current, Dictionary<T, T> CameFrom)
        {
            List<T> Path = new List<T>();
            Path.Add(Current);
            while (CameFrom.ContainsKey(Current))
            {
                Current = CameFrom[Current];
                Path.Insert(0, Current);
            }
            return Path;
        }

        /// Get a path from InitialState to GoalState.
        ///
        /// If no path is possible, return an empty enumerable.
        public IEnumerable<T> GetPath(T InitialState, T GoalState)
        {
            PriorityQueue<T, float> OpenQueue = new();
            HashSet<T> OpenSet = new HashSet<T>();
            Dictionary<T, T> CameFrom = new Dictionary<T, T>();
            Dictionary<T, float> GScore = new Dictionary<T, float>();
            Dictionary<T, float> FScore = new Dictionary<T, float>();

            GScore[InitialState] = 0;
            FScore[InitialState] = Heuristic(InitialState, GoalState);
            OpenSet.Add(InitialState);

            while (OpenSet.Count != 0)
            {
                // Note: could use sorted data structure for open set.
                T current = OpenSet.OrderBy(x => FScore.ContainsKey(x) ? FScore[x] : float.PositiveInfinity).First();
                OpenSet.Remove(current);

                // Check whether we have arrived.
                if (IsGoalFunc(current, GoalState))
                {
                    return ReconstructPath(current, CameFrom);
                }

                // Process the transitions
                T[] neighbours = GetTransitions(current);
                foreach (T neighbour in neighbours)
                {
                    float tentative_gscore = (GScore.ContainsKey(current) ? GScore[current] : float.PositiveInfinity) + GetEdgeWeight(current, neighbour);
                    float neighbour_gscore = GScore.GetValueOrDefault(neighbour, float.PositiveInfinity);
                    if (tentative_gscore < neighbour_gscore)
                    {
                        CameFrom[neighbour] = current;
                        GScore[neighbour] = tentative_gscore;
                        FScore[neighbour] = GScore[neighbour] + Heuristic(neighbour, GoalState);
                        if (!OpenSet.Contains(neighbour))
                        {
                            OpenSet.Add(neighbour);
                        }
                    }
                }
            }

            // No path is possible.
            return new T[] { };
        }
    }
}