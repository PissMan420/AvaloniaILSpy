﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Linq;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using SRM = System.Reflection.Metadata;

namespace ICSharpCode.ILSpy.TreeNodes
{
	public sealed class TypeTreeNode : ILSpyTreeNode, IMemberTreeNode
	{
		public TypeTreeNode(ITypeDefinition typeDefinition, AssemblyTreeNode parentAssemblyNode)
		{
			this.ParentAssemblyNode = parentAssemblyNode ?? throw new ArgumentNullException(nameof(parentAssemblyNode));
			this.TypeDefinition = typeDefinition ?? throw new ArgumentNullException(nameof(typeDefinition));
			this.LazyLoading = true;
		}

		public ITypeDefinition TypeDefinition { get; }

		public AssemblyTreeNode ParentAssemblyNode { get; }

		public override object Text => this.Language.TypeToString(TypeDefinition, includeNamespace: false)
			+ TypeDefinition.MetadataToken.ToSuffixString();

		public override bool IsPublicAPI {
			get {
				switch (TypeDefinition.Accessibility) {
					case Accessibility.Public:
					case Accessibility.Protected:
					case Accessibility.ProtectedOrInternal:
						return true;
					default:
						return false;
				}
			}
		}
		
		public override FilterResult Filter(FilterSettings settings)
		{
			if (!settings.ShowInternalApi && !IsPublicAPI)
				return FilterResult.Hidden;
			if (settings.SearchTermMatches(TypeDefinition.Name)) {
				if (settings.Language.ShowMember(TypeDefinition))
					return FilterResult.Match;
				else
					return FilterResult.Hidden;
			} else {
				return FilterResult.Recurse;
			}
		}
		
		protected override void LoadChildren()
		{
			if (TypeDefinition.DirectBaseTypes.Any())
				this.Children.Add(new BaseTypesTreeNode(ParentAssemblyNode.LoadedAssembly.GetPEFileOrNull(), TypeDefinition));
			if (!TypeDefinition.IsSealed)
				this.Children.Add(new DerivedTypesTreeNode(ParentAssemblyNode.AssemblyList, TypeDefinition));
			foreach (var nestedType in TypeDefinition.NestedTypes.OrderBy(t => t.Name, NaturalStringComparer.Instance)) {
				this.Children.Add(new TypeTreeNode(nestedType, ParentAssemblyNode));
			}
			foreach (var field in TypeDefinition.Fields.OrderBy(f => f.Name, NaturalStringComparer.Instance)) {
				this.Children.Add(new FieldTreeNode(field));
			}
			
			foreach (var property in TypeDefinition.Properties.OrderBy(p => p.Name, NaturalStringComparer.Instance)) {
				this.Children.Add(new PropertyTreeNode(property));
			}
			foreach (var ev in TypeDefinition.Events.OrderBy(e => e.Name, NaturalStringComparer.Instance)) {
				this.Children.Add(new EventTreeNode(ev));
			}
			foreach (var method in TypeDefinition.Methods.OrderBy(m => m.Name, NaturalStringComparer.Instance)) {
				if (method.MetadataToken.IsNil) continue;
				this.Children.Add(new MethodTreeNode(method));
			}
		}

		public override bool CanExpandRecursively => true;

		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.DecompileType(TypeDefinition, output, options);
		}

		public override object Icon => GetIcon(TypeDefinition);

		public static IBitmap GetIcon(ITypeDefinition type)
		{
			return Images.GetIcon(GetTypeIcon(type), GetOverlayIcon(type));
		}

		internal static TypeIcon GetTypeIcon(IType type)
		{
			switch (type.Kind) {
				case TypeKind.Interface:
					return TypeIcon.Interface;
				case TypeKind.Struct:
					return TypeIcon.Struct;
				case TypeKind.Delegate:
					return TypeIcon.Delegate;
				case TypeKind.Enum:
					return TypeIcon.Enum;
				default:
					if (type.GetDefinition()?.IsStatic == true)
						return TypeIcon.StaticClass;
					return TypeIcon.Class;
			}
		}

		static AccessOverlayIcon GetOverlayIcon(ITypeDefinition type)
		{
			switch (type.Accessibility) {
				case Accessibility.Public:
					return AccessOverlayIcon.Public;
				case Accessibility.Internal:
					return AccessOverlayIcon.Internal;
				case Accessibility.ProtectedAndInternal:
					return AccessOverlayIcon.PrivateProtected;
				case Accessibility.Protected:
				case Accessibility.ProtectedOrInternal:
					return AccessOverlayIcon.Protected;
				case Accessibility.Private:
					return AccessOverlayIcon.Private;
				default:
					return AccessOverlayIcon.CompilerControlled;
			}
		}

		IEntity IMemberTreeNode.Member => TypeDefinition;
	}
}