using UnityEngine;
using UnityEditor;

public class EnumMaskDrawer : MaterialPropertyDrawer {
    const int MAX_SAFE_FLOAT = (1 << 23) - 1;
    readonly string[] enumNames;

    #region Constructors
    public EnumMaskDrawer(string[] enumNames) => this.enumNames = enumNames;
    public EnumMaskDrawer(string name1
    ) : this(new[] { name1 }) { }
    public EnumMaskDrawer(string name1, string name2
    ) : this(new[] { name1, name2 }) { }
    public EnumMaskDrawer(string name1, string name2, string name3
    ) : this(new[] { name1, name2, name3 }) { }
    public EnumMaskDrawer(string name1, string name2, string name3, string name4
    ) : this(new[] { name1, name2, name3, name4 }) { }
    public EnumMaskDrawer(string name1, string name2, string name3, string name4, string name5
    ) : this(new[] { name1, name2, name3, name4, name5 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6
    ) : this(new[] { name1, name2, name3, name4, name5, name6 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7
    ) : this(new[] { name1, name2, name3, name4, name5, name6, name7 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8
    ) : this(new[] { name1, name2, name3, name4, name5, name6, name7, name8 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9
    ) : this(new[] { name1, name2, name3, name4, name5, name6, name7, name8, name9 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10
    ) : this(new[] { name1, name2, name3, name4, name5, name6, name7, name8, name9, name10 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11
    ) : this(new[] { name1, name2, name3, name4, name5, name6, name7, name8, name9, name10, name11 }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16, name17
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16, name17, name18
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19, string name20
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19, name20
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19, string name20,
        string name21
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19, name20, name21
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19, string name20,
        string name21, string name22
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19, name20, name21, name22
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19, string name20,
        string name21, string name22, string name23
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19, name20, name21, name22, name23
    }) { }
    public EnumMaskDrawer(
        string name1, string name2, string name3, string name4, string name5,
        string name6, string name7, string name8, string name9, string name10,
        string name11, string name12, string name13, string name14, string name15,
        string name16, string name17, string name18, string name19, string name20,
        string name21, string name22, string name23, string name24
    ) : this(new[] {
        name1, name2, name3, name4, name5, name6, name7, name8,
        name9, name10, name11, name12, name13, name14, name15, name16,
        name17, name18, name19, name20, name21, name22, name23, name24
    }) { }
    #endregion

    public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor) {
        EditorGUI.BeginChangeCheck();
        int value;
        try {
            value = (int)prop.floatValue;
        } catch {
            value = 0;
        }
        if ((value & MAX_SAFE_FLOAT) == MAX_SAFE_FLOAT) value = -1;
        value = EditorGUI.MaskField(position, label, value, enumNames) & MAX_SAFE_FLOAT;
        if (EditorGUI.EndChangeCheck())
            prop.floatValue = value;
    }
}
