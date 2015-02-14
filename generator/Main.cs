using System;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using Mono.Options;

class Generator
{
	public static void ShowHelp (OptionSet os)
	{
		Console.WriteLine ("Usage is: ");
		os.WriteOptionDescriptions (Console.Out);
		Console.WriteLine ("If no options are passed then this loads the various profiles in Mono");
		
		Environment.Exit (0);
	}

	static TextWriter writer;
	
	public static void Main(string[] args)
	{
		string assembly = null;
		string output = null;
		string directory = null;
		bool json = true;
		
		OptionSet os = null;
		
		os = new OptionSet () {
			{ "h|?|help", v => ShowHelp (os) },
			{ "a=|assembly=", v => assembly = v },
			{ "d=|directory=", v => directory = v },
			{ "xml", v => json = false },
			{ "json", v => json = true },
			{ "o=", v => output = v }
		};
		os.Parse (args);
		
		XDocument x = new XDocument ();
		XElement root = new XElement ("Root");
		x.Add (root);
		
		if (assembly != null){
			root.Add (LoadAssembly (assembly));
		} else if (directory != null) {
			root.Add (LoadProfile (Path.GetDirectoryName (directory), directory));
		} else {
			string d = Path.Combine (Path.GetDirectoryName (typeof (int).Assembly.CodeBase), "..").Substring (5);
			
			root.Add (LoadProfile (d + "/4.0", "4_00_Libs"));
		}

		if (json){
			writer = output == null ? Console.Out : File.CreateText (output);
			DumpAsJson (root);
			return;
		}
		
		if (output != null){
			using (var w = new XmlTextWriter (output, System.Text.Encoding.UTF8)){
				x.WriteTo (w);
			} 
		} else 
			Console.WriteLine (x);
		
	}

	static int level;
	public static void indent ()
	{
		for (int i = 0; i < level; i++)
			writer.Write ("  ");
	}
	
	public static void pn (string fmt, params object [] args)
	{
		indent ();
		if (args.Length == 0)
			writer.Write (fmt);
		else
			writer.Write (fmt, args);
	}

	public static void p (string fmt, params object [] args)
	{
		indent ();
		if (args.Length == 0)
			writer.WriteLine (fmt);
		else
			writer.WriteLine (fmt, args);
	}
	
	public static void DumpAsJson (XElement root)
	{
		bool needComma = false;
		foreach (var e in root.Elements ()){
			if (needComma)
				writer.WriteLine (",");
			p ("{"); level ++;
			var s = e.Attribute ("Size");
			var haveChild = e.Elements ().Count () > 0;
			
			p ("\"name\": \"{0}\"{1} ", e.Attribute ("Name").Value, (s != null || haveChild) ? "," : "");
			if (s != null){
				p ("\"size\": \"{0}\"{1}", s.Value, haveChild ? "," : "");
			}
			if (haveChild){
				p ("\"children\" : ["); level++;
				DumpAsJson (e);
				level--;
				p ("]");
			}
			level--;
			pn ("}");
			needComma = true;
		}
		writer.WriteLine ();
		if (level < 0)
			throw new Exception ();
	}
	
	public static XElement LoadProfile (string directory, string name)
	{
		Console.Error.WriteLine ("Loading directory {0}", directory);
		return new XElement ("Node", 
		              new XAttribute ("Name", name),
		              from d in Directory.GetFiles (directory)
		              where d.EndsWith (".dll")
		              select LoadAssembly (d));
	}
	
	public static string GetNamespace (TypeReference type)
	{
		if (type.IsNested)
			return GetNamespace (type.DeclaringType);
		else if (type.Namespace == "")
			return "<root>";
		else 
			return type.Namespace;
	}

	public static string GetName (TypeReference type)
	{
		if (type.IsNested)
			return GetName (type.DeclaringType) + "+" + type.Name;

		return type.Name;
	}
	
	public static XElement LoadAssembly (string s)
	{
		Console.Error.WriteLine ("    Loading assembly {0}", Path.GetFileName (s));
		AssemblyDefinition ad;
		
		try {
			 ad = TypeHelper.Resolver.Resolve (s);
		} catch {
			return new XElement ("Node", new XAttribute ("Name", "Failure to load"));
		}
		
		XElement xassembly = new XElement ("Node", 	new XAttribute ("Name", Path.GetFileName (s)));
		Dictionary<string,XElement> namespaces = new Dictionary<string, XElement> ();
		foreach (ModuleDefinition module in ad.Modules){			
			foreach (TypeDefinition type in module.Types){
				string ns = GetNamespace (type);

				if (namespaces.ContainsKey (ns))
					continue;
				
				XElement xnamespace = new XElement ("Node", new XAttribute ("Name", ns));
				xassembly.Add (xnamespace);
				namespaces [ns] = xnamespace;
			}
	
			foreach (TypeDefinition type in module.Types){
				XElement xtype = new XElement ("Node", new XAttribute ("Name", GetName (type)));
				XElement xnamespace = namespaces [GetNamespace (type)];
				
				xnamespace.Add (xtype);

				foreach (MethodDefinition method in type.Methods){
					MethodBody b = method.Body;
					
					if (b != null && method.Body.CodeSize > 0){
						XElement xmethod = new XElement ("Node", new XAttribute ("Name", method.Name));
						xmethod.Add (new XAttribute ("Size", method.Body.CodeSize));
					    
						xtype.Add (xmethod);
					}
				}
			}
		}
		return xassembly;
	}
	
}
