using System;

namespace FreeMote.Motion
{
    [Flags]
    public enum TexAttribute: ushort
    {
        None = 0,
        Translucent = 1,
        Opaque = 2,
        NoDiffuse = 4
    }

    [Flags]
    public enum MeshSyncChild : ushort
    {
        None = 0,
        Coord = 1,
        Angle = 2,
        Zoom = 4,
        Shape = 8
    }

    public enum MeshTransform : short
    {
        None = 0,
        BezierPatch = 1,
        Composite = 2
    }

    //it's a god-damn krkr enum
    internal enum TimelineFrameType: short
    {
        Null = 0,
        Single = 1,
        Continuous = 2,
        Tween = 3
    }


    // "///^(透過表示|ビュー|レイアウト|レイアウト角度|角度|XY座標|XY座標角度|Z座標|メッシュ|パーティクル|削除|ブレンドモード)\\((.+)\\)$"
    // "///^(Transparent|View|Layout|Layout角度|角度|XY座標|XY座標角度|Z座標|Mesh|Particle|Remove|BlendMode)\\((.+)\\)$"
    // Some parameters are compile-time constants (BlendMode, Remove etc), it's impossible to recover them if they were default value when compiling
    [Flags]
    public enum MmoFrameMask
    {
        //fx,fy, (flip) sx, sy, scc (slant), ccc (coord)
        //Low to High:
        //0: ox,oy
        //1: coord
        //2,3: fx, fy (must set both)
        //4: angle
        //5,6: zx,zy
        //7,8: sx,xy
        //9: color
        //10: opa
        //11: ccc
        //12: acc (angle)
        //13: zcc
        //14: scc     
        //15: occ
        //16: ???
        //17: bm
        //19: motion/timeOffset?
        //20: particle
        //25: mesh
        None = 0,

        /// <summary>
        /// ox, oy
        /// </summary>
        Origin = 1,

        /// <summary>
        /// coord
        /// </summary>
        Coord = 0b10,

        /// <summary>
        /// angle
        /// </summary>
        Angle = 0b10000,

        /// <summary>
        /// zx
        /// </summary>
        ZoomX = 0b100000,

        /// <summary>
        /// zy
        /// </summary>
        ZoomY = 0b1000000,

        /// <summary>
        /// color
        /// </summary>
        Color = 0b1000000000,

        /// <summary>
        /// opa
        /// </summary>
        Opacity = 0b10000000000,

        /// <summary>
        /// ccc
        /// </summary>
        CoordCurve = 0b10_0000000000,

        /// <summary>
        /// acc
        /// </summary>
        AngleCurve = 0b100_0000000000,

        /// <summary>
        /// zcc
        /// </summary>
        ZoomCurve = 0b1000_0000000000,

        /// <summary>
        /// scc
        /// </summary>
        SlantCurve = 0b10000_0000000000,

        /// <summary>
        /// occ (color cc)
        /// </summary>
        OpacityCurve = 0b100000_0000000000,

        /// <summary>
        /// bm
        /// </summary>
        BlendMode = 0b10000000_0000000000,

        /// <summary>
        /// motion
        /// </summary>
        Motion = 0b1000000000_0000000000,

        /// <summary>
        /// prt
        /// </summary>
        Particle = 0b1_0000000000_0000000000,

        /// <summary>
        /// mesh
        /// </summary>
        Mesh = 0b100000_0000000000_0000000000,
    }
}
