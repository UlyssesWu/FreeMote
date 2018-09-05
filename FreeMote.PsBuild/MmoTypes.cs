﻿using System;
// ReSharper disable InconsistentNaming

namespace FreeMote.PsBuild
{
    /*particle in frameList/content
     *                "prt": {
                      "amax": 5,
                      "amin": 4,
                      "fmax": 10.0,
                      "fmin": 1.0,
                      "mask": 63,
                      "range": 8,
                      "trigger": 1,
                      "vmax": 0.05,
                      "vmin": 0.0333333351,
                      "zmax": 7,
                      "zmin": 6
                    },
     */

    [Flags]
    internal enum MmoFrameMaskEx
    {
        None = 0,
        CoordXY = 0b1,
        CoordZ = 0b10,
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
    public enum MmoMarkerColor
    {
        /// <summary>
        /// なし
        /// </summary>
        None = 0,
        /// <summary>
        /// 赤
        /// </summary>
        Red = 1,
        /// <summary>
        /// 绿
        /// </summary>
        Green = 2,
        /// <summary>
        /// 青
        /// </summary>
        Blue = 3,
        /// <summary>
        /// 橙
        /// </summary>
        Orange = 4,
        /// <summary>
        /// 紫
        /// </summary>
        Purple = 5,
        /// <summary>
        /// 桃
        /// </summary>
        Pink = 6,
        /// <summary>
        /// 灰
        /// </summary>
        Gray = 7,
    }
    public enum MmoItemClass
    {
        ObjLayerItem = 0, //CharaItem, MotionItem
        //"objClipping": 0,
        //"objMaskThresholdOpacity": 64,
        //"objTriPriority": 2,
        //TextLayer is always hold by a ObjLayer with "#text00000" label and "src/#font00000/#text00000" frameList/content/src
        ShapeLayerItem = 1,
        //"shape": "point" (psb: 0) | "circle" (psb: 1) | "rect" (psb: 2) | "quad" (psb: 3)
        LayoutLayerItem = 2,
        MotionLayerItem = 3,
        /*
          "motionClipping": 0,
          "motionIndependentLayerInherit": 0,
          "motionMaskThresholdOpacity": 64,
         */
        ParticleLayerItem = 4,
        //"particle": "point" (psb: 0) | "ellipse" (psb: 1) | "quad" (psb: 2) 
        /*                                 
          "particleAccelRatio": 1.0,
          "particleApplyZoomToVelocity": 0,
          "particleDeleteOutsideScreen": 0,
          "particleFlyDirection": 0,
          "particleInheritAngle": 0,
          "particleInheritOpacity": 1,
          "particleInheritVelocity": 0,
          "particleMaxNum": 20,
          "particleMotionList": [],
          "particleTriVolume": 0,
         */

        CameraLayerItem = 5,
        ClipLayerItem = 7, //nothing special
        TextLayerItem = 8,
        /*
        "fontParams": {
        "antiAlias": 1,
        "bold": 0,
        "brushColor1": -16777216,
        "brushColor2": -16777216,
        "depth": 1,
        "name": "ＭＳ ゴシック",
        "penColor": -16777216,
        "penSize": 0,
        "rev": 1,
        "size": 16
        },
        "textParams": {
        "alignment": 0,
        "colSpace": 0,
        "defaultVertexColor": -1,
        "originAlignment": 1,
        "rasterlize": 2,
        "rowSpace": 0,
        "text": "Built by FreeMote"
        },
         */
        MeshLayerItem = 11, //nothing special, take care of meshXXX
        StencilLayerItem = 12,
        /*
          "stencilCompositeMaskLayerList": [],
          "stencilMaskThresholdOpacity": 64,
          "stencilType": 1,
         */
    }

    /// <summary>
    /// MMO Meta Format for PSD
    /// </summary>
    public class MmoPsdMetadata
    {
        public string SourceLabel { get; set; }
        public string PsdComment { get; set; }
        public string PsdFrameLabel { get; set; }
        public string PsdGroup { get; set; }
        /// <summary>
        /// Category
        /// <para>WARNING: Category can not be set as `Expression`</para>
        /// </summary>
        public string Category { get; set; }
        public string Label { get; set; }
    }
}