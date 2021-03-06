//
// MIT X11 licensed.
// Author: Miguel de Icaza
// Copyright 2009 Novell, Inc.
// 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using Moonlight.Gtk;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Moonlight;

class MainClass
{
	const int width = 1024;
	const int height = 768;
		
	public static void Main(string[] args)
	{
		Node n = null;
		
		if (args.Length == 0){
			Console.WriteLine ("Must specify the XML file with the data to load");
			return;
		}
		try {
			n = LoadNodes (args [0], "Size", "Foo");
		} catch {
			Console.WriteLine ("Unable to load {0}", args [0]);
			throw;
		}
		
		Gtk.Application.Init ();
		MoonlightRuntime.Init ();
		
		Gtk.Window w = new Gtk.Window ("Foo");
		var v = new Gtk.VBox (false, 0);
		
		w.DeleteEvent += delegate { 
			Gtk.Application.Quit ();
		};
		
		w.SetSizeRequest (width, height);
		MoonlightHost h = new MoonlightHost ();

		v.PackStart (h, true, true, 0);
		var back = new Gtk.Button ("Back");
		v.PackStart (back, false, false, 0);
		w.Add (v);
		
		w.ShowAll ();

		// Make it pretty, skip all levels that are just 1 element
		while (n.Children.Count == 1)
			n = n.Children [0];
		
		// Render
		TreemapRenderer r = new TreemapRenderer (n, "");
		r.KeyDown += delegate (object sender, KeyEventArgs e) {
			if (e.Key == Key.Back)
				r.Back ();
		};

		back.Clicked += delegate {
			r.Back ();
		};
		
		SetSize (r, width, height);
		
		h.Application.RootVisual = r;
		
		w.ResizeChecked += delegate(object sender, EventArgs e) {
			int new_width, new_heigth;
			w.GetSize (out new_width, out new_heigth);
			
			SetSize (r, new_width, new_heigth);;
		};
		
		Gtk.Application.Run ();
	}
	
	static void SetSize (TreemapRenderer r, int width, int height)
	{
		Size available = new Size (width, height);
		r.Measure (available);
		r.Arrange (new Rect (0, 0, width, height));
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
	
	static XName xn_name = XName.Get ("Name", "");
	
	public static Node LoadNodes (string s, string dim1, string dim2)
	{
		XDocument d = XDocument.Load (s);
		XElement root = ((XElement) d.FirstNode);
		XName d1 = XName.Get (dim1, "");
		XName d2 = XName.Get (dim2, "");
		
		return LoadNodes (root, d1, d2);
	}

	public static void Update (XElement xe, ref int v, XName k)
	{
		
		var attr = xe.Attribute (k);
		if (attr != null){
			int r;
			
			if (int.TryParse (attr.Value, out r))
				v += r;
		}
	}
	
	public static Node LoadNodes (XElement xe, XName k1, XName k2)
	{
		XAttribute xa = xe.Attribute (xn_name);
		
		Node n = new Node (xe.Nodes ().Count ());
		if (xa != null)
			n.Name = xa.Value;
		
		Update (xe, ref n.Size, k1);
		Update (xe, ref n.Value, k2);
			
		foreach (XNode e in xe.Nodes ()){
			if (e is XElement){
				Node child = LoadNodes ((XElement) e, k1, k2);
				n.Size += child.Size;
				n.Value += child.Value;
				
				n.Children.Add (child);
			}
		}
		
		return n;
	}
}
