//
// MIT X11 licensed.
// Author: Miguel de Icaza
// Copyright 2009 Novell, Inc.
// 
using System;
using System.Collections;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Xml;
using System.Linq;
using Moonlight.Gtk;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Animation;

public class Node : IComparer <Node> {
	public string Name { get; set; }
	public int    Size { get; set; }
	public int    Area { get; set; }
	public List<Node> Children;
	public Rect Rect { get; set; }
	
	public int Compare (Node a, Node b)
	{
		int n = b.Size - a.Size;
		if (n != 0)
			return n;
		return string.Compare (b.Name, a.Name);
	}
	
	public Node Clone ()
	{
		return new Node (this);
	}
	
	public Node () 
	{
		Children = new List<Node> ();
	}
	
	public Node (Node re)
	{
		Name = re.Name;
		Size = re.Size;
		Area = re.Area;
		Rect = re.Rect;
		
		Children = new List<Node> (re.Children.Count);
		foreach (Node rec in re.Children)
			Children.Add (rec);
				 
	}
}

class MainClass
{
	const int width = 1024;
	const int height = 768;
		
	public static void Main(string[] args)
	{
		Node n = null;
		
		if (args.Length == 0){
			try {
				n = LoadAssembly ("/mono/lib/mono/2.0/mscorlib.dll");
			} catch {
				Console.WriteLine ("Pass an assembly to explore");
				return;
			}
		}
		if (n == null){
			try {
				n = LoadAssembly (args [0]);
			} catch {
				Console.WriteLine ("Unable to load {0}", args [0]);
				return;
			}		
		}
		
		Gtk.Application.Init ();
		MoonlightRuntime.Init ();
		
		Gtk.Window w = new Gtk.Window ("Foo");
		w.DeleteEvent += delegate { 
			Gtk.Application.Quit ();
		};
		
		w.SetSizeRequest (width, height);
		MoonlightHost h = new MoonlightHost ();
		w.Add (h);
		w.ShowAll ();

		
		Canvas container = new Canvas (){
			Width = width,
			Height = height,
			Background = new SolidColorBrush (Color.FromArgb (255, 0x4c, 0x4c, 0x4c))
		};
			
		Canvas c = new TreemapRenderer (n, new Rect (0, 0, width - 20, height - 20), "");
		Canvas.SetTop (c, 10);
		Canvas.SetLeft (c, 10);
		container.Children.Add (c);
		
		h.Application.RootVisual = container;
		Gtk.Application.Run ();
	}
	
	static void DumpNode (Node n, int indent)
	{
		for (int i = 0; i < indent; i++)
			Console.Write (" ");
		p ("{0} => {1}", n.Name, n.Size);
		foreach (Node child in n.Children){
			DumpNode (child, (indent+1) * 2);
		}
	}
	
	
	static void p (string format, params object [] args)
	{
		Console.WriteLine (format, args);
	}
	
	public static Node LoadAssembly (string s)
	{
		Node nassembly = new Node () { Name = s };
		
		AssemblyDefinition ad = TypeHelper.Resolver.Resolve (s);
		
		foreach (ModuleDefinition module in ad.Modules){
			var nsnodes = new Dictionary <string,Node> ();
			
			foreach (TypeDefinition type in module.Types){
				if (nassembly.Children.FindIndex (x => x.Name == type.Namespace) != -1)
					continue;
				
				Node nnamespace = new Node () { Name = type.Namespace };
				if (nnamespace.Name == "")
					nnamespace.Name = "<root>";
				nassembly.Children.Add (nnamespace);
				nsnodes [type.Namespace] = nnamespace;
			
			}
			
			foreach (TypeDefinition type in module.Types){
				Node ntype = new Node () { Name = type.Name };
				Node nnamespace = nsnodes [type.Namespace];
				nnamespace.Children.Add (ntype);
				
				foreach (MethodDefinition method in type.Methods){
					MethodBody b = method.Body;
					
					if (b != null && method.Body.CodeSize > 0){
					    Node nmethod = new Node () { Name = method.Name, Size = method.Body.CodeSize };
						ntype.Children.Add (nmethod);
						ntype.Size += method.Body.CodeSize;
					}
				}
				nnamespace.Size += ntype.Size;
				nassembly.Size += ntype.Size;
			}
		}
		return nassembly;
	}
}

public class TreemapRenderer : Canvas {
	Node root;
	Rect region;
	string caption;
	
	Brush borderBrush = new SolidColorBrush (Colors.White);
	Brush backgroundBrush = new SolidColorBrush (Color.FromArgb (0xff, 0x4c, 0x4c, 0x4c));
	Brush transparentBrush = new SolidColorBrush (Colors.Transparent);
	
	public TreemapRenderer (Node source, Rect region, string caption)
	{
		Width = region.Width;
		Height = region.Height;
		Console.WriteLine ("Treemap for {0}", source.Children.Count);
		this.root = source.Clone ();
		this.region = region;
		this.Background = backgroundBrush;
		this.caption = caption;
		
		Sort (root);
		
		SetRegion (region);
	}
	
	public void SetRegion (Rect region)
	{
		Children.Clear ();
		
		if (caption != ""){
			int max;
			string formatted = MakeCaption (caption, out max);
			double w = region.Width * 1.60;
			double s = w / max;
				
			var text = new TextBlock () {
				FontSize = s,
				Text = formatted,
				Foreground = new SolidColorBrush (Colors.Gray)
			};
			SetTop (text, (region.Height-text.ActualHeight)/2);
			SetLeft (text, (region.Width-text.ActualWidth)/2);
			Children.Add (text);
		}
		
		var border = new Rectangle () { 
			Width = region.Width, 
			Height = region.Height, 
			Stroke = borderBrush,
			Opacity = 1.0,
			StrokeThickness = 1
				};
		
		Canvas.SetLeft (border, region.X);
		Canvas.SetTop (border, region.Y);
		Children.Add (border);
				
		Rect emptyArea = region;
		Squarify (emptyArea, root.Children);
		
		Plot (root.Children);
	}

	const int PADX = 5;
	const int PADY = 3;
	
	void Plot (List<Node> children)
	{
		Random r = new Random (10);
		
		Console.WriteLine ("Plotting {0}", children.Count);
		Canvas foo;

			
		foreach (Node child in children){
			Canvas host = new Canvas ();
			
			host.Width = child.Rect.Width;
			host.Height = child.Rect.Height;
			SetLeft (host, child.Rect.X);
			SetTop (host, child.Rect.Y);
			host.Background = transparentBrush;
			                                                       
			Children.Add (host);	
			
			// Create box + other stuff
			
			var rect = new System.Windows.Shapes.Rectangle ();
			rect.Width = child.Rect.Width;
			rect.Height = child.Rect.Height;			
			rect.Stroke = borderBrush;
			rect.Opacity = 1.0;
			rect.StrokeThickness = 1;
					
			host.Children.Add (rect);
			
			TextBlock text = null;
			// 
			// The text
			//
			if (child.Rect.Height > PADY*2 || child.Rect.Width > PADX*2 + 20){
				int max;
				string caption = MakeCaption (child.Name, out max);
				
				// fudge
				double w = child.Rect.Width * 1.60;
				double s = w / max;
				
				text = new TextBlock () {
					FontSize = s,
					Text = caption,
					FontFamily = new FontFamily ("Candara"),
					//Clip = new RectangleGeometry () { Rect = textClip },
					Foreground = borderBrush,
					
				};
				
				if (text.ActualHeight > child.Rect.Height){
					text.FontSize = text.FontSize / 2;
				}

				SetTop (text, (child.Rect.Height - text.ActualHeight)/2);
				SetLeft (text, (child.Rect.Width - text.ActualWidth)/2);
				host.Children.Add (text);
			}

			bool inside = false;
			host.MouseEnter += delegate {
				host.Background = new SolidColorBrush (Colors.Yellow);
				if (text != null)
					text.Foreground = new SolidColorBrush (Colors.Black);
				inside = true;
			};
			
			host.MouseLeave += delegate {
				host.Background = transparentBrush;
				if (text != null)
					text.Foreground = borderBrush;
				inside = false;
			}; 
			
			//
			// Click handling
			//
			if (child.Children.Count == 0)
				continue;
			
			bool pressed = false;
			host.MouseLeftButtonDown += delegate {
				pressed = true;
			}; 
			
			Node child_copy = child;
			
			host.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
				bool click = pressed && inside;
				pressed = inside = false;

				e.Handled = true;
				
				if (click)
					Clicked (child_copy);
			};

		}
	}
	
	// Render a child
	void Clicked (Node n)
	{
		Canvas c = new TreemapRenderer (n, region, n.Name);
		c.Width = region.Width;
		c.Height = region.Height;

		var xlate = new TranslateTransform () {
					X = n.Rect.X,
					Y = n.Rect.Y };
		
		var scale = new ScaleTransform () {
					ScaleX = n.Rect.Width / region.Width,
					ScaleY = n.Rect.Height / region.Height };

		c.RenderTransform = new TransformGroup { Children = { scale, xlate } };
		c.Opacity = 0.5;
		
		// Animations
		TimeSpan t = TimeSpan.FromSeconds (0.3);
		
		var anim_x = new DoubleAnimation () {
			Duration = t,
			To = 0 }; 
		var anim_y = new DoubleAnimation () {
			Duration = t,
			To = 0 }; 
		var anim_sx = new DoubleAnimation (){
			Duration = t,
			To = 1.0 };
		var anim_sy = new DoubleAnimation (){
			Duration = t,
			To = 1.0 };
		var anim_opacity = new DoubleAnimation () {
			Duration = t,
			To = 1.0 };
		var anim_opacity_parent = new DoubleAnimation () {
			Duration = t,
			From = 1.0,
			To = 0.0 };
		
		Storyboard.SetTarget (anim_x, xlate);
		Storyboard.SetTargetProperty (anim_x, new PropertyPath ("X"));
		Storyboard.SetTarget (anim_y, xlate);
		Storyboard.SetTargetProperty (anim_y, new PropertyPath ("Y"));
		Storyboard.SetTarget (anim_sx, scale);
		Storyboard.SetTargetProperty (anim_sx, new PropertyPath ("ScaleX"));
		Storyboard.SetTarget (anim_sy, scale);
		Storyboard.SetTargetProperty (anim_sy, new PropertyPath ("ScaleY"));
		Storyboard.SetTarget (anim_opacity, c);
		Storyboard.SetTargetProperty (anim_opacity, new PropertyPath ("Opacity"));

		var s = new Storyboard () { Children = { anim_x, anim_y, anim_sx, anim_sy, anim_opacity }};

		s.Begin ();
		
		
		
		c.MouseRightButtonUp += delegate (object sender, MouseButtonEventArgs e){
			e.Handled = true;
			Console.WriteLine ("Up");
			Children.Remove (c);
		};
		Children.Add (c);
	}

	static string MakeCaption (string s, out int max)
	{
		string [] elements = s.Split (new char [] {'.'});
		
		max = 0;
		foreach (string el in elements)
			if (el.Length > max)
				max = el.Length;
		return string.Join ("\n", elements);
	}
	public static double GetShortestSide (Rect r)
	{
		return Math.Min (r.Width, r.Height);
	}
	
	static void Squarify (Rect emptyArea, List<Node> children)
	{
		double fullArea = 0;
		foreach (Node child in children){
			fullArea += child.Size;
		}
		
		double area = emptyArea.Width * emptyArea.Height;
		foreach (Node child in children){
			child.Area = (int) (area * child.Size / fullArea);
		}
		
		Squarify (emptyArea, children, new List<Node> (), GetShortestSide (emptyArea));
		
		foreach (Node child in children){
			if (child.Area < 9000 || child.Children.Count == 0){
				//Console.WriteLine ("Passing on this {0} {1} {2}", child.Area, child.Children, child.Children.Count);
				continue;
			}
			
			Squarify (child.Rect, child.Children);
		}
	}
	
	static void Squarify (Rect emptyArea, List<Node> children, List<Node> row, double w)
	{
		if (children.Count == 0){
			AddRowToLayout (emptyArea, row);
			return;
		}
		
		Node head = children [0];
		
		List<Node> row_plus_head = new List<Node> (row);
		row_plus_head.Add (head);
		
		double worst1 = Worst (row, w);
		double worst2 = Worst (row_plus_head, w);
		                      
		if (row.Count == 0 || worst1 > worst2){
			List<Node> children_tail = new List<Node> (children);
			children_tail.RemoveAt (0);
			Squarify (emptyArea, children_tail, row_plus_head, w);
		} else {
			emptyArea = AddRowToLayout (emptyArea, row);
			Squarify (emptyArea, children, new List<Node>(), GetShortestSide (emptyArea));
		}
	}
	
	static double Worst (List<Node> row, double sideLength)
	{
		if (row.Count == 0)
			return 0;
		
		double maxArea = 0, minArea = double.MaxValue;
		double totalArea  = 0;
		foreach (Node n in row){
			maxArea = Math.Max (maxArea, n.Area);
			minArea = Math.Min (minArea, n.Area);
			totalArea += n.Area;
		}
		
		if (minArea == double.MaxValue)
			minArea = 0;
		
		double v1 = (sideLength * sideLength * maxArea) / (totalArea * totalArea);
		double v2 = (totalArea * totalArea) / (sideLength * sideLength * minArea);
		
		return Math.Max (v1, v2);
	}
	
	static Rect AddRowToLayout (Rect emptyArea, List<Node> row)
	{
		Rect result;
		double areaUsed = 0;
		foreach (Node n in row)
			areaUsed += n.Area;
		
		if (emptyArea.Width > emptyArea.Height){
			double w = areaUsed / emptyArea.Height;
			result = new Rect (emptyArea.X + w, emptyArea.Y, Math.Max (0, emptyArea.Width - w), emptyArea.Height);
			
			double y = emptyArea.Y;
			foreach (Node n in row){
				double h = n.Area * emptyArea.Height / areaUsed;
				
				n.Rect = new Rect (emptyArea.X, y, w, h);
				//Console.WriteLine ("       PLACE Item {0}->{1}", n.Name, n.Rect);
				//Console.WriteLine ("Slot {0} with {1} got {2}", n.Name, n.Size, n.Rect);
				y += h;
			}
		} else {
			double h = areaUsed / emptyArea.Width;
			//Console.WriteLine ("   Height > Width: {0}", h);
			result = new Rect (emptyArea.X, emptyArea.Y + h, emptyArea.Width, Math.Max (0, emptyArea.Height - h));
			
			double x = emptyArea.X;
			foreach (Node n in row){
				double w = n.Area * emptyArea.Width / areaUsed;
				n.Rect = new Rect (x, emptyArea.Y, w, h);
				x += w;
			}
		}
		
		return result;
	}
	
	static void Sort (Node n)
	{
		n.Children.Sort (n);
		foreach (Node child in n.Children)
			Sort (child);
	}
	
}