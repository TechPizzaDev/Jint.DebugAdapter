using Acornima;
using Acornima.Ast;
using Jint.DebugAdapter.BreakPoints;

namespace Jint.DebugAdapter
{
    public class ScriptInfo
    {
        private List<Position> breakPointPositions;

        public string SourceId { get; }
        public Program Ast { get; }
        public List<Position> BreakPointPositions => breakPointPositions ??= CollectBreakPointPositions();

        public ScriptInfo(string sourceId, Program ast)
        {
            SourceId = sourceId;
            Ast = ast;
        }

        public IEnumerable<Position> FindBreakPointPositionsInRange(Position start, Position end)
        {
            var positions = BreakPointPositions;
            var index = positions.BinarySearch(start);
            if (index < 0)
            {
                // Get the first break after the location
                index = ~index;
            }

            while (index < positions.Count)
            {
                var position = positions[index++];
                // We know we're past the start of the range. If we're also past the end, break
                if (position.CompareTo(end) > 0)
                {
                    break;
                }

                yield return position;
            }
        }

        public Position FindNearestBreakPointPosition(Position position)
        {
            var positions = BreakPointPositions;
            var index = positions.BinarySearch(position);
            if (index < 0)
            {
                // Get the first break after the location
                index = ~index;
            }
            return positions[index];
        }

        private List<Position> CollectBreakPointPositions()
        {
            var collector = new BreakPointCollector();
            collector.Visit(Ast);
            // Some statements may be at the same location
            var list = collector.Positions.Distinct().ToList();
            // We need the list sorted (it's going to be used for binary search)
            list.Sort();
            return list;
        }
    }
}
