using System;

namespace Jither.SourceGen.Helpers;

[Flags]
public enum IndentedBlockFlags
{
    None = 0,

    LineBeforePrefix = 1 << 0,
    LineBeforeSuffix = 1 << 1,
    LineBeforeBoth = LineBeforePrefix | LineBeforeSuffix,

    LineAfterPrefix = 1 << 2,
    LineAfterSuffix = 1 << 3,
    LineAfterBoth = LineAfterPrefix | LineAfterSuffix,

    ZeroWidth = 1 << 4,

    Default = LineAfterBoth,
}
