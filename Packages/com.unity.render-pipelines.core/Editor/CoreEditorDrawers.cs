using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor.AnimatedValues;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering
{
    /// <summary>display options added to the Foldout</summary>
    [Flags]
    public enum FoldoutOption
    {
        /// <summary>No Option</summary>
        None = 0,
        /// <summary>Foldout will be indented</summary>
        Indent = 1 << 0,
        /// <summary>Foldout will be boxed</summary>
        Boxed = 1 << 2,
        /// <summary>Foldout will be inside another foldout</summary>
        SubFoldout = 1 << 3,
        /// <summary>Remove the space at end of the foldout</summary>
        NoSpaceAtEnd = 1 << 4,
        /// <summary>Foldout backgound will use the same as for TitleHeaders</summary>
        TitleHeader = 1 << 5,
    }

    /// <summary>display options added to the Group</summary>
    [Flags]
    public enum GroupOption
    {
        /// <summary>No Option</summary>
        None = 0,
        /// <summary>Group will be indented</summary>
        Indent = 1 << 0
    }

    /// <summary>
    /// Utility class to draw inspectors
    /// </summary>
    /// <typeparam name="TData">Type of class containing data needed to draw inspector</typeparam>
    public static class CoreEditorDrawer<TData>
    {
        /// <summary> Abstraction that have the Draw hability </summary>
        public interface IDrawer
        {
            /// <summary>
            /// The draw function
            /// </summary>
            /// <param name="serializedProperty">The SerializedProperty to draw</param>
            /// <param name="owner">The editor handling this draw call</param>
            void Draw(TData serializedProperty, Editor owner);

            /// <summary>
            /// Expands all children that use a given mask
            /// </summary>
            /// <param name="mask">The mask to expand</param>
            /// <returns>If the drawer is expanded</returns>
            bool Expand(int mask);
        }

        /// <summary>Delegate that must say if this is enabled for drawing</summary>
        /// <param name="data">The data used</param>
        /// <param name="owner">The editor rendering</param>
        /// <returns>True if this should be drawn</returns>
        public delegate bool Enabler(TData data, Editor owner);

        /// <summary>Delegate is called when the foldout state is switched</summary>
        /// <param name="data">The data used</param>
        /// <param name="owner">The editor rendering</param>
        public delegate void SwitchEnabler(TData data, Editor owner);

        /// <summary>Delegate that must be used to select sub object for data for drawing</summary>
        /// <typeparam name="T2Data">The type of the sub object used for data</typeparam>
        /// <param name="data">The data used</param>
        /// <param name="owner">The editor rendering</param>
        /// <returns>Embeded object that will be used as data source for later draw in this Select</returns>
        public delegate T2Data DataSelect<T2Data>(TData data, Editor owner);

        /// <summary>Delegate type alternative to IDrawer</summary>
        /// <param name="data">The data used</param>
        /// <param name="owner">The editor rendering</param>
        public delegate void ActionDrawer(TData data, Editor owner);

        /// <summary> Equivalent to EditorGUILayout.Space that can be put in a drawer group </summary>
        public static readonly IDrawer space = Group((data, owner) => EditorGUILayout.Space());

        /// <summary> Use it when IDrawer required but no operation should be done </summary>
        public static readonly IDrawer noop = Group((data, owner) => { });

        internal static bool DefaultExpand(ActionDrawer[] actionDrawers, int mask)
        {
            for (var i = 0; i < actionDrawers.Length; i++)
            {
                if (actionDrawers[i] == null)
                    continue;
                var targets = (actionDrawers[i].Target as IDrawer[]);
                if (targets == null)
                    continue;
                foreach (var target in targets)
                {
                    if (target.Expand(mask))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Conditioned drawer that will only be drawn if its enabler function is null or return true
        /// </summary>
        /// <param name="enabler">Enable the drawing if null or return true</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Conditional(Enabler enabler, params IDrawer[] contentDrawers)
        {
            return new ConditionalDrawerInternal(enabler, contentDrawers.Draw);
        }

        /// <summary>
        /// Conditioned drawer that will only be drawn if its enabler function is null or return true
        /// </summary>
        /// <param name="enabler">Enable the drawing if null or return true</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Conditional(Enabler enabler, params ActionDrawer[] contentDrawers)
        {
            return new ConditionalDrawerInternal(enabler, contentDrawers);
        }

        class ConditionalDrawerInternal : IDrawer
        {
            ActionDrawer[] actionDrawers { get; set; }
            Enabler m_Enabler;

            public ConditionalDrawerInternal(Enabler enabler = null, params ActionDrawer[] actionDrawers)
            {
                this.actionDrawers = actionDrawers;
                m_Enabler = enabler;
            }

            void IDrawer.Draw(TData data, Editor owner)
            {
                if (m_Enabler != null && !m_Enabler(data, owner))
                    return;

                for (var i = 0; i < actionDrawers.Length; i++)
                    actionDrawers[i](data, owner);
            }

            bool IDrawer.Expand(int mask) => DefaultExpand(actionDrawers, mask);
        }

        internal static IDrawer ConditionalWithAdditionalProperties(Enabler enabler, AnimFloat animation, params IDrawer[] contentDrawers)
        {
            return new ConditionalDrawerWithAdditionalPropertiesInternal(enabler, animation, contentDrawers.Draw);
        }

        internal static IDrawer ConditionalWithAdditionalProperties(Enabler enabler, AnimFloat animation, params ActionDrawer[] contentDrawers)
        {
            return new ConditionalDrawerWithAdditionalPropertiesInternal(enabler, animation, contentDrawers);
        }

        class ConditionalDrawerWithAdditionalPropertiesInternal : IDrawer
        {
            ActionDrawer[] m_ActionDrawers { get; set; }
            Enabler m_Enabler;
            AnimFloat m_Anim;

            public ConditionalDrawerWithAdditionalPropertiesInternal(Enabler enabler = null, AnimFloat anim = null, params ActionDrawer[] actionDrawers)
            {
                m_ActionDrawers = actionDrawers;
                m_Enabler = enabler;
                m_Anim = anim;
            }

            void IDrawer.Draw(TData data, Editor owner)
            {
                if (m_Enabler != null && !m_Enabler(data, owner))
                    return;

                if (AdvancedProperties.BeginGroup(m_Anim))
                {
                    for (var i = 0; i < m_ActionDrawers.Length; i++)
                        m_ActionDrawers[i](data, owner);
                }
                AdvancedProperties.EndGroup();
            }

            bool IDrawer.Expand(int mask) => DefaultExpand(m_ActionDrawers, mask);
        }

        /// <summary>
        /// Conditioned drawer that will draw something depending of the return of the switch
        /// </summary>
        /// <param name="switch">Chose witch drawing to use</param>
        /// <param name="drawIfTrue">This will be draw if the <see cref="switch"/> is true</param>
        /// <param name="drawIfFalse">This will be draw if the <see cref="switch"/> is false</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer TernaryConditional(Enabler @switch, IDrawer drawIfTrue, IDrawer drawIfFalse)
            => new TernaryConditionalDrawerInternal(@switch, drawIfTrue.Draw, drawIfFalse.Draw);

        /// <summary>
        /// Conditioned drawer that will draw something depending of the return of the switch
        /// </summary>
        /// <param name="switch">Chose witch drawing to use</param>
        /// <param name="drawIfTrue">This will be draw if the <see cref="switch"/> is true</param>
        /// <param name="drawIfFalse">This will be draw if the <see cref="switch"/> is false</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer TernaryConditional(Enabler @switch, ActionDrawer drawIfTrue, ActionDrawer drawIfFalse)
            => new TernaryConditionalDrawerInternal(@switch, drawIfTrue, drawIfFalse);

        class TernaryConditionalDrawerInternal : IDrawer
        {
            ActionDrawer drawIfTrue;
            ActionDrawer drawIfFalse;
            Enabler m_Switch;

            public TernaryConditionalDrawerInternal(Enabler @switch, ActionDrawer drawIfTrue, ActionDrawer drawIfFalse)
            {
                this.drawIfTrue = drawIfTrue;
                this.drawIfFalse = drawIfFalse;
                m_Switch = @switch;
            }

            void IDrawer.Draw(TData data, Editor owner)
            {
                if (m_Switch != null && !m_Switch(data, owner))
                    drawIfFalse?.Invoke(data, owner);
                else
                    drawIfTrue?.Invoke(data, owner);
            }

            bool IDrawer.Expand(int mask) => DefaultExpand(new ActionDrawer[] { drawIfTrue, drawIfFalse }, mask);
        }

        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, null, GroupOption.None, contentDrawers.Draw);
        }

        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, null, GroupOption.None, contentDrawers);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(float labelWidth, params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(labelWidth, null, GroupOption.None, contentDrawers.Draw);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="header">Adds a header on top <see cref="GUIContent"/></param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GUIContent header, params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, header, GroupOption.None, contentDrawers.Draw);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(float labelWidth, params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(labelWidth, null, GroupOption.None, contentDrawers);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="header">Adds a header on top <see cref="GUIContent"/></param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GUIContent header, params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, header, GroupOption.None, contentDrawers);
        }

        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GroupOption options, params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, null, options, contentDrawers.Draw);
        }

        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GroupOption options, params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, null, options, contentDrawers);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(float labelWidth, GroupOption options, params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(labelWidth, null, options, contentDrawers.Draw);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="header">Adds a header on top <see cref="GUIContent"/></param>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GUIContent header, GroupOption options, params IDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1f, header, options, contentDrawers.Draw);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(float labelWidth, GroupOption options, params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(labelWidth, null, options, contentDrawers);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="header">Adds a header on top <see cref="GUIContent"/></param>
        /// <param name="options">Allow to add indentation on this group</param>
        /// <param name="contentDrawers">The content of the group</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Group(GUIContent header, GroupOption options, params ActionDrawer[] contentDrawers)
        {
            return new GroupDrawerInternal(-1, header, options, contentDrawers);
        }

        class GroupDrawerInternal : IDrawer
        {
            ActionDrawer[] actionDrawers { get; set; }
            GUIContent header { get; }
            float m_LabelWidth;
            bool isIndented;

            public GroupDrawerInternal(float labelWidth = -1f, GUIContent header = null, GroupOption options = GroupOption.None, params ActionDrawer[] actionDrawers)
            {
                this.actionDrawers = actionDrawers;
                this.header = header;
                m_LabelWidth = labelWidth;
                isIndented = (options & GroupOption.Indent) != 0;
            }

            void IDrawer.Draw(TData data, Editor owner)
            {
                if (isIndented)
                    ++EditorGUI.indentLevel;
                var currentLabelWidth = EditorGUIUtility.labelWidth;
                if (m_LabelWidth >= 0f)
                {
                    EditorGUIUtility.labelWidth = m_LabelWidth;
                }
                if (header != null)
                    EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

                for (var i = 0; i < actionDrawers.Length; i++)
                    actionDrawers[i](data, owner);

                if (m_LabelWidth >= 0f)
                {
                    EditorGUIUtility.labelWidth = currentLabelWidth;
                }
                if (isIndented)
                    --EditorGUI.indentLevel;
            }

            bool IDrawer.Expand(int mask) => DefaultExpand(actionDrawers, mask);
        }

        class FoldoutGroupDrawerInternal<TEnum> : IDrawer
            where TEnum : struct, IConvertible
        {
            readonly ActionDrawer[] m_ActionDrawers;

            readonly bool m_IsBoxed;
            readonly bool m_IsSubFoldout;
            readonly bool m_NoSpaceAtEnd;
            readonly bool m_IsIndented;
            readonly bool m_IsTitleHeader;

            readonly GUIContent m_Title;
            readonly string m_HelpUrl;

            ExpandedStateBase<TEnum> m_State;
            readonly TEnum m_Mask;

            readonly Enabler m_Enabler;
            readonly SwitchEnabler m_SwitchEnabler;

            Action<GenericMenu, TData> m_customMenuContextAction;

            public FoldoutGroupDrawerInternal(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state,
                                              Enabler enabler, SwitchEnabler switchEnabler, FoldoutOption options = FoldoutOption.None, Action<GenericMenu, TData> customMenuContextAction = null, string otherDocumentation = null, params ActionDrawer[] actionDrawers)
            {
                m_IsBoxed = (options & FoldoutOption.Boxed) != 0;
                m_IsIndented = (options & FoldoutOption.Indent) != 0;
                m_IsSubFoldout = (options & FoldoutOption.SubFoldout) != 0;
                m_NoSpaceAtEnd = (options & FoldoutOption.NoSpaceAtEnd) != 0;
                m_IsTitleHeader = (options & FoldoutOption.TitleHeader) != 0;

                m_ActionDrawers = actionDrawers;
                m_Title = title;
                m_State = state;
                m_Mask = mask;

                m_customMenuContextAction = customMenuContextAction;

                if (otherDocumentation != null)
                {
                    m_HelpUrl = otherDocumentation;
                }
                else
                {
                    m_HelpUrl = DocumentationUtils.GetHelpURL<TEnum>(mask);
                }

                m_Enabler = enabler;
                m_SwitchEnabler = switchEnabler;
            }

            void IDrawer.Draw(TData data, Editor owner)
            {
                bool expended = m_State[m_Mask];
                bool newExpended;

                if (m_IsSubFoldout)
                {
                    newExpended = CoreEditorUtils.DrawSubHeaderFoldout(m_Title, expended, m_IsBoxed);
                }
                else
                {
                    CoreEditorUtils.DrawSplitter(m_IsBoxed);
                    newExpended = CoreEditorUtils.DrawHeaderFoldout(m_Title,
                        expended,
                        m_IsBoxed,
                        m_Enabler == null ? null : () => m_Enabler(data, owner),
                        m_SwitchEnabler == null ? null : () => m_SwitchEnabler(data, owner),
                        m_IsTitleHeader,
                        m_HelpUrl,
                        null,
                        m_customMenuContextAction == null ? null : (menu) => m_customMenuContextAction(menu, data));
                    if (m_IsTitleHeader)
                        CoreEditorUtils.DrawFoldoutEndSplitter(m_IsBoxed);
                }
                if (newExpended ^ expended)
                    m_State[m_Mask] = newExpended;
                if (!newExpended)
                    return;

                if (m_IsIndented)
                    ++EditorGUI.indentLevel;
                for (var i = 0; i < m_ActionDrawers.Length; i++)
                    m_ActionDrawers[i](data, owner);
                if (m_IsIndented)
                    --EditorGUI.indentLevel;
                if (!m_NoSpaceAtEnd)
                    EditorGUILayout.Space();
            }

            bool IDrawer.Expand(int mask)
            {
                bool expand = (mask == (int)(m_Mask as object));
                if (!expand)
                    expand = DefaultExpand(m_ActionDrawers, mask);

                if (expand)
                    m_State[m_Mask] = true;
                return expand;
            }
        }

        /// <summary> Create an IDrawer based on an other data container </summary>
        /// <typeparam name="T2Data">Type of selected object containing in the given data containing data needed to draw inspector</typeparam>
        /// <param name="dataSelect">The data new source for the inner drawers</param>
        /// <param name="otherDrawers">Inner drawers drawed with given data sources</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Select<T2Data>(
            DataSelect<T2Data> dataSelect,
            params CoreEditorDrawer<T2Data>.IDrawer[] otherDrawers)
        {
            return new SelectDrawerInternal<T2Data>(dataSelect, otherDrawers.Draw);
        }

        /// <summary> Create an IDrawer based on an other data container </summary>
        /// <typeparam name="T2Data">Type of selected object containing in the given data containing data needed to draw inspector</typeparam>
        /// <param name="dataSelect">The data new source for the inner drawers</param>
        /// <param name="otherDrawers">Inner drawers drawed with given data sources</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer Select<T2Data>(
            DataSelect<T2Data> dataSelect,
            params CoreEditorDrawer<T2Data>.ActionDrawer[] otherDrawers)
        {
            return new SelectDrawerInternal<T2Data>(dataSelect, otherDrawers);
        }

        class SelectDrawerInternal<T2Data> : IDrawer
        {
            DataSelect<T2Data> m_DataSelect;
            CoreEditorDrawer<T2Data>.ActionDrawer[] m_SourceDrawers;

            public SelectDrawerInternal(DataSelect<T2Data> dataSelect,
                                        params CoreEditorDrawer<T2Data>.ActionDrawer[] otherDrawers)
            {
                m_SourceDrawers = otherDrawers;
                m_DataSelect = dataSelect;
            }

            void IDrawer.Draw(TData data, Editor o)
            {
                var p2 = m_DataSelect(data, o);
                for (var i = 0; i < m_SourceDrawers.Length; i++)
                    m_SourceDrawers[i](p2, o);
            }

            bool IDrawer.Expand(int mask) => false;
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(EditorGUIUtility.TrTextContent(title), mask, state, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(EditorGUIUtility.TrTextContent(title), mask, state, options, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, string otherDocumentation, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, otherDocumentation, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(EditorGUIUtility.TrTextContent(title), mask, state, options, customMenuContextAction, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(string title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, string otherDocumentation, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(EditorGUIUtility.TrTextContent(title), mask, state, options, customMenuContextAction, otherDocumentation, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, FoldoutOption.Indent, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, FoldoutOption.Indent, null, null, null, null, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, null, null, null, null, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, string otherDocumentation, params IDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, otherDocumentation, contentDrawers.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, null, null, null, contentDrawers);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedStateBase </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="state">The ExpandedStateBase describing the component</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="contentDrawers">The content of the foldout header</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, string otherDocumentation, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, customMenuContextAction, null, null, otherDocumentation, contentDrawers);
        }

        // This one is private as we do not want to have unhandled advanced switch. Change it if necessary.
        static IDrawer FoldoutGroup<TEnum>(GUIContent title, TEnum mask, ExpandedStateBase<TEnum> state, FoldoutOption options, Action<GenericMenu, TData> customMenuContextAction, Enabler showAdditionalProperties, SwitchEnabler switchAdditionalProperties, string otherDocumentation, params ActionDrawer[] contentDrawers)
            where TEnum : struct, IConvertible
        {
            return new FoldoutGroupDrawerInternal<TEnum>(title, mask, state, showAdditionalProperties, switchAdditionalProperties, options, customMenuContextAction, otherDocumentation, contentDrawers);
        }

        /// <summary> Helper to draw a foldout with an advanced switch on it. </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <typeparam name="TState">Type of the persistent state</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="isAdvanced"> Delegate allowing to check if advanced mode is active. </param>
        /// <param name="switchAdvanced"> Delegate to know what to do when advance is switched. </param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="advancedContent"> The content of the foldout header only visible if advanced mode is active and if foldout is expended. </param>
        /// <param name="options">Drawing options</param>
        /// <returns>A IDrawer object</returns>
        [Obsolete("Use AdditionalPropertiesFoldoutGroup instead. #from(2021.2)")]
        public static IDrawer AdvancedFoldoutGroup<TEnum, TState>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedState<TEnum, TState> foldoutState, Enabler isAdvanced, SwitchEnabler switchAdvanced, IDrawer normalContent, IDrawer advancedContent, FoldoutOption options = FoldoutOption.Indent) where TEnum : struct, IConvertible
        {
            return AdvancedFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, isAdvanced, switchAdvanced, normalContent.Draw, advancedContent.Draw, options);
        }

        /// <summary> Helper to draw a foldout with an advanced switch on it. </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <typeparam name="TState">Type of the persistent state</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="isAdvanced"> Delegate allowing to check if advanced mode is active. </param>
        /// <param name="switchAdvanced"> Delegate to know what to do when advance is switched. </param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="advancedContent"> The content of the foldout header only visible if advanced mode is active and if foldout is expended. </param>
        /// <param name="options">Drawing options</param>
        /// <returns>A IDrawer object</returns>
        [Obsolete("Use AdditionalPropertiesFoldoutGroup instead. #from(2021.2)")]
        public static IDrawer AdvancedFoldoutGroup<TEnum, TState>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedState<TEnum, TState> foldoutState, Enabler isAdvanced, SwitchEnabler switchAdvanced, ActionDrawer normalContent, IDrawer advancedContent, FoldoutOption options = FoldoutOption.Indent) where TEnum : struct, IConvertible
        {
            return AdvancedFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, isAdvanced, switchAdvanced, normalContent, advancedContent.Draw, options);
        }

        /// <summary> Helper to draw a foldout with an advanced switch on it. </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <typeparam name="TState">Type of the persistent state</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="isAdvanced"> Delegate allowing to check if advanced mode is active. </param>
        /// <param name="switchAdvanced"> Delegate to know what to do when advance is switched. </param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="advancedContent"> The content of the foldout header only visible if advanced mode is active and if foldout is expended. </param>
        /// <param name="options">Drawing options</param>
        /// <returns>A IDrawer object</returns>
        [Obsolete("Use AdditionalPropertiesFoldoutGroup instead. #from(2021.2)")]
        public static IDrawer AdvancedFoldoutGroup<TEnum, TState>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedState<TEnum, TState> foldoutState, Enabler isAdvanced, SwitchEnabler switchAdvanced, IDrawer normalContent, ActionDrawer advancedContent, FoldoutOption options = FoldoutOption.Indent)
            where TEnum : struct, IConvertible
        {
            return AdvancedFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, isAdvanced, switchAdvanced, normalContent.Draw, advancedContent, options);
        }

        /// <summary> Helper to draw a foldout with an advanced switch on it. </summary>
        /// <typeparam name="TEnum">Type of the mask used</typeparam>
        /// <typeparam name="TState">Type of the persistent state</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="isAdvanced"> Delegate allowing to check if advanced mode is active. </param>
        /// <param name="switchAdvanced"> Delegate to know what to do when advance is switched. </param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="advancedContent"> The content of the foldout header only visible if advanced mode is active and if foldout is expended. </param>
        /// <param name="options">Drawing options</param>
        /// <returns>A IDrawer object</returns>
        [Obsolete("Use AdditionalPropertiesFoldoutGroup instead. #from(2021.2)")]
        public static IDrawer AdvancedFoldoutGroup<TEnum, TState>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedState<TEnum, TState> foldoutState, Enabler isAdvanced, SwitchEnabler switchAdvanced, ActionDrawer normalContent, ActionDrawer advancedContent, FoldoutOption options = FoldoutOption.Indent) where TEnum : struct, IConvertible
        {
            return FoldoutGroup(foldoutTitle, foldoutMask, foldoutState, options, null, isAdvanced, switchAdvanced, null, normalContent,
                Conditional((serialized, owner) => isAdvanced(serialized, owner) && foldoutState[foldoutMask], advancedContent).Draw);
        }

        /// <summary>
        /// Helper to draw a foldout with additional properties.
        /// </summary>
        /// <typeparam name="TEnum">Type of the foldout mask used.</typeparam>
        /// <typeparam name="TAPEnum">Type of the additional properties mask used.</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="additionalPropertiesMask">Bit mask (enum) used to define the boolean saving the state in AdditionalPropertiesStateBase</param>
        /// <param name="additionalPropertiesState">The AdditionalPropertiesStateBase describing the component</param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="additionalContent">The content of the foldout header only visible if additional properties are shown and if foldout is expanded.</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer AdditionalPropertiesFoldoutGroup<TEnum, TAPEnum>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedStateBase<TEnum> foldoutState,
            TAPEnum additionalPropertiesMask, AdditionalPropertiesStateBase<TAPEnum> additionalPropertiesState, IDrawer normalContent, IDrawer additionalContent, FoldoutOption options = FoldoutOption.Indent, Action<GenericMenu, TData> customMenuContextAction = null, string otherDocumentation = null)
            where TEnum : struct, IConvertible
            where TAPEnum : struct, IConvertible
        {
            additionalContent ??= Group((s, o) => { });
            return AdditionalPropertiesFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, additionalPropertiesMask, additionalPropertiesState, normalContent.Draw, additionalContent.Draw, options, customMenuContextAction, otherDocumentation);
        }

        /// <summary>
        /// Helper to draw a foldout with additional properties.
        /// </summary>
        /// <typeparam name="TEnum">Type of the foldout mask used.</typeparam>
        /// <typeparam name="TAPEnum">Type of the additional properties mask used.</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="additionalPropertiesMask">Bit mask (enum) used to define the boolean saving the state in AdditionalPropertiesStateBase</param>
        /// <param name="additionalPropertiesState">The AdditionalPropertiesStateBase describing the component</param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="additionalContent">The content of the foldout header only visible if additional properties are shown and if foldout is expanded.</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer AdditionalPropertiesFoldoutGroup<TEnum, TAPEnum>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedStateBase<TEnum> foldoutState,
            TAPEnum additionalPropertiesMask, AdditionalPropertiesStateBase<TAPEnum> additionalPropertiesState, ActionDrawer normalContent, IDrawer additionalContent, FoldoutOption options = FoldoutOption.Indent, Action<GenericMenu, TData> customMenuContextAction = null, string otherDocumentation = null)
            where TEnum : struct, IConvertible
            where TAPEnum : struct, IConvertible
        {
            return AdditionalPropertiesFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, additionalPropertiesMask, additionalPropertiesState, normalContent, additionalContent.Draw, options, customMenuContextAction, otherDocumentation);
        }

        /// <summary>
        /// Helper to draw a foldout with additional properties.
        /// </summary>
        /// <typeparam name="TEnum">Type of the foldout mask used.</typeparam>
        /// <typeparam name="TAPEnum">Type of the additional properties mask used.</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="additionalPropertiesMask">Bit mask (enum) used to define the boolean saving the state in AdditionalPropertiesStateBase</param>
        /// <param name="additionalPropertiesState">The AdditionalPropertiesStateBase describing the component</param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="additionalContent">The content of the foldout header only visible if additional properties are shown and if foldout is expanded.</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer AdditionalPropertiesFoldoutGroup<TEnum, TAPEnum>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedStateBase<TEnum> foldoutState,
            TAPEnum additionalPropertiesMask, AdditionalPropertiesStateBase<TAPEnum> additionalPropertiesState, IDrawer normalContent, ActionDrawer additionalContent, FoldoutOption options = FoldoutOption.Indent, Action<GenericMenu, TData> customMenuContextAction = null, string otherDocumentation = null)
            where TEnum : struct, IConvertible
            where TAPEnum : struct, IConvertible
        {
            return AdditionalPropertiesFoldoutGroup(foldoutTitle, foldoutMask, foldoutState, additionalPropertiesMask, additionalPropertiesState, normalContent.Draw, additionalContent, options, customMenuContextAction, otherDocumentation);
        }

        /// <summary>
        /// Helper to draw a foldout with additional properties.
        /// </summary>
        /// <typeparam name="TEnum">Type of the foldout mask used.</typeparam>
        /// <typeparam name="TAPEnum">Type of the additional properties mask used.</typeparam>
        /// <param name="foldoutTitle">Title wanted for this foldout header</param>
        /// <param name="foldoutMask">Bit mask (enum) used to define the boolean saving the state in ExpandedStateBase</param>
        /// <param name="foldoutState">The ExpandedStateBase describing the component</param>
        /// <param name="additionalPropertiesMask">Bit mask (enum) used to define the boolean saving the state in AdditionalPropertiesStateBase</param>
        /// <param name="additionalPropertiesState">The AdditionalPropertiesStateBase describing the component</param>
        /// <param name="normalContent"> The content of the foldout header always visible if expended. </param>
        /// <param name="additionalContent">The content of the foldout header only visible if additional properties are shown and if foldout is expanded.</param>
        /// <param name="options">Drawing options</param>
        /// <param name="customMenuContextAction">Adds Addtional items to the menu activated from the burger menu.</param>
        /// <param name="otherDocumentation">Custom documentation used for header.</param>
        /// <returns>A IDrawer object</returns>
        public static IDrawer AdditionalPropertiesFoldoutGroup<TEnum, TAPEnum>(GUIContent foldoutTitle, TEnum foldoutMask, ExpandedStateBase<TEnum> foldoutState,
            TAPEnum additionalPropertiesMask, AdditionalPropertiesStateBase<TAPEnum> additionalPropertiesState, ActionDrawer normalContent, ActionDrawer additionalContent, FoldoutOption options = FoldoutOption.Indent, Action<GenericMenu, TData> customMenuContextAction = null, string otherDocumentation = null)
            where TEnum : struct, IConvertible
            where TAPEnum : struct, IConvertible
        {
            bool Enabler(TData data, Editor owner)
            {
                return additionalPropertiesState[additionalPropertiesMask];
            }

            void SwitchEnabler(TData data, Editor owner)
            {
                additionalPropertiesState[additionalPropertiesMask] = !additionalPropertiesState[additionalPropertiesMask];
            }

            return FoldoutGroup(foldoutTitle, foldoutMask, foldoutState, options, customMenuContextAction, Enabler, SwitchEnabler,
                otherDocumentation, normalContent,
                ConditionalWithAdditionalProperties(
                    (serialized, owner) => additionalPropertiesState[additionalPropertiesMask] && foldoutState[foldoutMask],
                    AdvancedProperties.s_AnimFloat,
                    additionalContent).Draw
            );
        }
    }

    /// <summary>CoreEditorDrawer extensions</summary>
    public static class CoreEditorDrawersExtensions
    {
        /// <summary> Concatenate a collection of IDrawer as a unique IDrawer </summary>
        /// <typeparam name="TData">Type of class containing data needed to draw inspector</typeparam>
        /// <param name="drawers">A collection of IDrawers</param>
        /// <param name="data">The data source for the inner drawers</param>
        /// <param name="owner">The editor drawing</param>
        public static void Draw<TData>(this IEnumerable<CoreEditorDrawer<TData>.IDrawer> drawers, TData data, Editor owner)
        {
            EditorGUILayout.BeginVertical();
            foreach (var drawer in drawers)
                drawer.Draw(data, owner);
            EditorGUILayout.EndVertical();
        }

        internal static bool Expand<TData>(this IEnumerable<CoreEditorDrawer<TData>.IDrawer> drawers, int mask)
        {
            foreach (var drawer in drawers)
            {
                if (drawer.Expand(mask))
                    return true;
            }
            return false;
        }
    }
}
