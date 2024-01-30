﻿//This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/4.0/ or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Diagnostics;
using FreeMote.Psb;

// ReSharper disable InconsistentNaming

namespace FreeMote.PsBuild
{
    internal static class MmoExtensions
    {
        /// <summary>
        /// Convert number to shape
        /// </summary>
        /// <param name="shape"></param>
        /// <returns></returns>
        public static string ToShapeString(this PsbNumber shape)
        {
            if (Enum.IsDefined(typeof(MmoShape), shape.IntValue))
            {
                return ((MmoShape) shape.IntValue).ToString();
            }

            Debug.WriteLine($"{shape.IntValue} is not a valid {nameof(MmoShape)}");
            return MmoShape.point.ToString();
        }
    }

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

    internal enum MmoShape
    {
        point = 0,
        circle = 1,
        rect = 2,
        quad = 3,
    }

    [Flags]
    internal enum MmoFrameMaskEx
    {
        None = 0,
        CoordXY = 0b1,
        CoordZ = 0b10,
        SrcSrc = 0b100,
        SrcMotion = 0b1000,
        SrcShape = 0b10000,
        SrcParticle = 0b100000,
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
        ParticleLayerItem = 4, //global.LAYER_TYPE_PARTICLE = (int)4
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
        ModelLayerItem = 6,
        ClipLayerItem = 7, //nothing special
        TextLayerItem = 8,
        AnchorLayerItem = 9,
        FeedbackLayerItem = 10,
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

    public partial class MmoBuilder
    {
    }
}