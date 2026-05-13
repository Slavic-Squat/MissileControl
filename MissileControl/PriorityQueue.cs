using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class PriorityQueue<T, TPriority> where TPriority : IComparable<TPriority>
        {
            private List<T> _heap;
            private readonly Func<T, TPriority> _prioritySelector;

            public int Count => _heap.Count;

            public PriorityQueue(Func<T, TPriority> prioritySelector)
            {
                if (prioritySelector == null)
                    throw new ArgumentNullException(nameof(prioritySelector));

                _prioritySelector = prioritySelector;
                _heap = new List<T>();
            }

            public PriorityQueue(Func<T, TPriority> prioritySelector, int capacity)
            {
                if (prioritySelector == null)
                    throw new ArgumentNullException(nameof(prioritySelector));

                _prioritySelector = prioritySelector;
                _heap = new List<T>(capacity);
            }

            public PriorityQueue(Func<T, TPriority> prioritySelector, IEnumerable<T> items)
            {
                if (prioritySelector == null)
                    throw new ArgumentNullException(nameof(prioritySelector));

                _prioritySelector = prioritySelector;
                _heap = new List<T>(items);

                // Build heap from bottom up
                for (int i = (_heap.Count / 2) - 1; i >= 0; i--)
                {
                    BubbleDown(i);
                }
            }

            public void Enqueue(T item)
            {
                _heap.Add(item);
                BubbleUp(_heap.Count - 1);
            }

            public T Dequeue()
            {
                if (_heap.Count == 0)
                    throw new InvalidOperationException("Priority queue is empty.");

                T min = _heap[0];
                int lastIndex = _heap.Count - 1;
                _heap[0] = _heap[lastIndex];
                _heap.RemoveAt(lastIndex);

                if (_heap.Count > 0)
                    BubbleDown(0);

                return min;
            }

            public T Peek()
            {
                if (_heap.Count == 0)
                    throw new InvalidOperationException("Priority queue is empty.");

                return _heap[0];
            }

            public void Clear()
            {
                _heap.Clear();
            }

            public bool Contains(T item)
            {
                return _heap.Contains(item);
            }

            private int Compare(T item1, T item2)
            {
                return _prioritySelector(item1).CompareTo(_prioritySelector(item2));
            }

            private void BubbleUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;

                    if (Compare(_heap[index], _heap[parentIndex]) >= 0)
                        break;

                    Swap(index, parentIndex);
                    index = parentIndex;
                }
            }

            private void BubbleDown(int index)
            {
                int lastIndex = _heap.Count - 1;

                while (true)
                {
                    int leftChild = 2 * index + 1;
                    int rightChild = 2 * index + 2;
                    int smallest = index;

                    if (leftChild <= lastIndex && Compare(_heap[leftChild], _heap[smallest]) < 0)
                        smallest = leftChild;

                    if (rightChild <= lastIndex && Compare(_heap[rightChild], _heap[smallest]) < 0)
                        smallest = rightChild;

                    if (smallest == index)
                        break;

                    Swap(index, smallest);
                    index = smallest;
                }
            }

            private void Swap(int i, int j)
            {
                T temp = _heap[i];
                _heap[i] = _heap[j];
                _heap[j] = temp;
            }
        }
    }
}
