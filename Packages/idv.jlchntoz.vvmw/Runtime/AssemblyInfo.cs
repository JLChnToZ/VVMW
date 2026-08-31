using System.Runtime.CompilerServices;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
[assembly: InternalsVisibleTo("JLChnToZ.VVMW.Editor")]
[assembly: EditorI18NSource(LanguageAssetPaths = new[] {
    "Packages/idv.jlchntoz.vvmw/Resources/lang.json",
    "Packages/idv.jlchntoz.vvmw/Resources/editor-lang.json"
})]
#if VRCLV2_IMPORTED
[assembly: DeclareDefine("VRC_LIGHT_VOLUMES_V2")]
#else
[assembly: DeclareUndefine("VRC_LIGHT_VOLUMES_V2")]
#endif
#if VRCLV3_IMPORTED
[assembly: DeclareDefine("VRC_LIGHT_VOLUMES_V3")]
#else
[assembly: DeclareUndefine("VRC_LIGHT_VOLUMES_V3")]
#endif
