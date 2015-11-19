﻿/****************************************************************************
**
**  Developer: Caleb Amoa Buahin, Utah State University
**  Email: caleb.buahin@aggiemailgmail.com
** 
**  This file is part of the Flood-Forecasting-Tool.exe, a flood inundation forecasting tool was created as part of a project for the National
**  Flood Interoperability Experiment (NFIE) Summer Institute held at the National Water Center at University of Alabama Tuscaloosa from June 1st through July 17.
**  Special thanks to the following project members who made significant contributed to the approaches used in this code and its testing.
**  Nikhil Sangwan, Purdue University, Indiana
**  Cassandra Fagan, University of Texas, Austin
**  Samuel Rivera, University of Illinois at Urbana-Champaign
**  Curtis Rae, Brigham Young University, Utah
**  Marc Girons-Lopez Uppsala University, Sweden
**  Special thanks to our advisors, Dr.Jeffery Horsburgh, Dr. Jim Nelson, and Dr. Maidment who were instrumetal to the success of this project
**  Flood-Forecasting-Tool.exe and its associated files is free software; you can redistribute it and/or modify
**  it under the terms of the Lesser GNU General Public License as published by
**  the Free Software Foundation; either version 3 of the License, or
**  (at your option) any later version.
**
**  Flood-Forecasting-Tool.exe and its associated files is distributed in the hope that it will be useful,
**  but WITHOUT ANY WARRANTY; without even the implied warranty of
**  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**  Lesser GNU General Public License for more details.
**
**  You should have received a copy of the Lesser GNU General Public License
**  along with this program.  If not, see <http://www.gnu.org/licenses/>
**
****************************************************************************/

using DotSpatial.Data;
using DotSpatial.Topology;
using RAS41;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;


[XmlRootAttribute(IsNullable = false, ElementName = "HECRASRatingCurve")]
public class HecRasModel
{
    # region variables

    private HECRASController controller;
    private List<string> profiles;


    private static string[] spaceDel = new string[] { " " }, commaDel = new string[] { "," }, equalDel = new string[] { "=" }, colonDel = new string[] { ":" };
    private SerializableDictionary<string, River> rivers;
    private string projectFile;

    #endregion

    public HecRasModel()
    {
        rivers = new SerializableDictionary<string,River>("RiverReachDictionaryItem", "Key", "Value");
        profiles = new List<string>();
        controller = new HECRASController();
        controller.Compute_HideComputationWindow();

    }

    public HecRasModel(FileInfo projectFile)
    {
        controller = new HECRASController();
        controller.Compute_HideComputationWindow();

        if (File.Exists(projectFile.FullName))
        {
            this.projectFile = projectFile.FullName;
            OpenHECRASProjectFile();

        }
        else
        {
            throw new FileNotFoundException("Project file was not found", projectFile.FullName);
        }

        rivers = new SerializableDictionary<string, River>("RiverReachDictionaryItem", "Key", "Value");
        profiles = new List<string>();
    }

    #region properties

    [XmlIgnore()]
    public HECRASController Controller
    {
        get { return controller; }
        //set { controller = value; }
    }

    public SerializableDictionary<string, River> Rivers
    {
        get { return rivers; }
        set { rivers = value; }
    }

    [XmlAttribute()]
    public string ProjectFile
    {
        get { return projectFile; }
        set
        {
            projectFile = value;
            //openHECRASProjectFile();
        }
    }

    [XmlIgnore]
    public List<string> Profiles
    {
        get { return profiles; }
        set { profiles = value; }
    }

    # endregion

    # region functions

    public void ReadRivers()
    {
        //clear previous data
        rivers.Clear();

        //
        string gml = controller.Geometry_GetGML(controller.CurrentGeomFile());

        //Parse xml from gml
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(gml);

        IList<XmlNode> nlist = doc.GetElementsByTagName("River").Cast<XmlNode>().Where(n => n.ChildNodes.Count > 1).ToList();

        foreach (XmlNode n in nlist)
        {
            string riverId = n["River"].InnerText;
            string reachId = n["Reach"].InnerText ;
            River r = null;
            
            if(rivers.ContainsKey(riverId))
            {
                r = rivers[riverId];
            }
            else
            {
                r = new River(riverId);
                rivers.Add(riverId, r);
            }

            if (!r.Reaches.ContainsKey(riverId))
            {
                List<XmlNode> crosssectionsOnReach = doc.GetElementsByTagName("XS").Cast<XmlNode>()
                    .Where(p => p["River"].InnerText == riverId && p["Reach"].InnerText == reachId).ToList();

                if (crosssectionsOnReach.Count > 0)
                {
                    Reach reach = new Reach(reachId);

                    for (int i = 0; i < crosssectionsOnReach.Count; i++)
                    {
                        XmlNode xs = crosssectionsOnReach[i];

                        string cutLineCoordsText = xs["geometryProperty"]["gml:LineString"]["gml:coordinates"].InnerText;
                        string[] cutLineCoords = cutLineCoordsText.Split(spaceDel, StringSplitOptions.RemoveEmptyEntries);

                        XSection section = new XSection(xs["RiverStation"].InnerText);

                        for (int j = 0; j < cutLineCoords.Length; j++)
                        {
                            string[] tCoords = cutLineCoords[j].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);
                            section.XSCutLine.Add(new Point(double.Parse(tCoords[0]), double.Parse(tCoords[1]), 0.0));
                        }

                        reach.XSections.Add(section.StationName.Trim(), section);
                    }

                    r.Reaches.Add(reachId, reach);
                }
            }
        }


        string geomFIle = controller.CurrentGeomFile();

        using (TextReader reader = new StreamReader(geomFIle))
        {
            string line;
            Reach currentReach = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("River Reach="))
                {
                    string[] args = line.Split(equalDel, StringSplitOptions.RemoveEmptyEntries);
                    args = args[1].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);

                    if(args.Length > 1)
                    {
                        string riverID = args[0];
                        string reachID = args[1];

                        if(rivers.ContainsKey(riverID))
                        {
                            River river = rivers[riverID];

                            if(river.Reaches.ContainsKey(reachID))
                            {
                                currentReach = river.Reaches[reachID];
                            }
                        }
                    }

                }
                else if (line.Contains("Type RM Length") && currentReach != null)
                {
                    string[] args = line.Split(equalDel, StringSplitOptions.RemoveEmptyEntries);

                    args = args[1].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);

                    bool end = false;

                    string xsectionID = args[1].Trim();

                    List<double> xpathElevationPoints = new List<double>();

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("#Sta/Elev"))
                        {
                            while ((line = reader.ReadLine()) != null)
                            {
                                int start = 0;
                                double value = 0;

                                while (start < line.Length)
                                {
                                    string token = "";

                                    if (start + 8 < line.Length)
                                    {
                                        token = line.Substring(start, 8);
                                        start += 8;
                                    }
                                    else
                                    {
                                        int length = line.Length - start;
                                        token = line.Substring(start, length);
                                        start += length;
                                    }

                                    if (double.TryParse(token, out value))
                                    {
                                        xpathElevationPoints.Add(value);
                                    }
                                    else
                                    {
                                        end = true;
                                        break;
                                    }
                                }

                                if (end)
                                {
                                    break;
                                }
                            }
                        }

                        if (end)
                        {
                            break;
                        }
                    }

                    currentReach.XSections[xsectionID].SetElevationPoints(xpathElevationPoints);
                }
            }
        }


        foreach (River r in rivers.Values)
            foreach(Reach re in r.Reaches.Values)
            re.CreateBoundingPolygon();
    }

    public void ReadProfiles(bool export = true)
    {

        profiles.Clear();

        if (export)
        {
            //export .sdf
            controller.ExportGIS();
        }

        FileInfo prj = new FileInfo(controller.CurrentProjectFile());

        string sdfFile = prj.FullName.Replace(prj.Extension, ".RASExport.sdf");

        //check to make sure .sdf file was really exported
        if (File.Exists(sdfFile))
        {
            //Reading .sdf files
            using (TextReader reader = new StreamReader(sdfFile))
            {
                string line = "";

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("NUMBER OF PROFILES:"))
                    {
                        string[] cols = line.Split(colonDel, StringSplitOptions.RemoveEmptyEntries);
                        int count = 0;

                        if (cols.Length > 1 && int.TryParse(cols[1], out count))
                        {
                            line = reader.ReadLine();

                            for (int i = 0; i < count; i++)
                            {
                                line = reader.ReadLine();
                                profiles.Add(line.Trim());
                            }
                        }
                    }
                    else if (line.Trim() == "CROSS-SECTION:")
                    {
                        line = reader.ReadLine();
                        string[] cols = line.Split(colonDel, StringSplitOptions.RemoveEmptyEntries);
                        River river = rivers[cols[1].Trim()];

                        line = reader.ReadLine();
                        cols = line.Split(colonDel, StringSplitOptions.RemoveEmptyEntries);
                        Reach reach = river.Reaches[cols[1].Trim()];

                        line = reader.ReadLine();
                        cols = line.Split(colonDel, StringSplitOptions.RemoveEmptyEntries);

                        XSection xs = reach.XSections[cols[1].Trim()];

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains("WATER ELEVATION"))
                            {
                                cols = line.Split(colonDel, StringSplitOptions.RemoveEmptyEntries);
                                cols = cols[1].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);

                                for (int i = 0; i < profiles.Count; i++)
                                {
                                    xs.ProfileElevations.Add(profiles[i], double.Parse(cols[i]));
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    public void ReadSteadyStateFlowData()
    {
        string steadystateFile = controller.CurrentSteadyFile();

        if (File.Exists(steadystateFile))
        {
            using (TextReader reader = new StreamReader(steadystateFile))
            {
                string line = "";
                List<string> tempProfiles = new List<string>();

                XSection current = null;
                double flow = 0.0;
                int count = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("Profile Names"))
                    {
                        string[] cols = line.Split(equalDel, StringSplitOptions.RemoveEmptyEntries);

                        cols = cols[1].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in cols)
                        {
                            tempProfiles.Add(s.Trim());
                        }
                    }
                    else if (line.Contains("River Rch & RM"))
                    {
                        string[] cols = line.Split(equalDel, StringSplitOptions.RemoveEmptyEntries);
                        cols = cols[1].Split(commaDel, StringSplitOptions.RemoveEmptyEntries);

                        if (cols.Length > 2)
                        {
                            current = rivers[cols[0].Trim()].Reaches[cols[1].Trim()].XSections[cols[2].Trim()];
                            count = 0;
                        }
                        else
                        {
                            current = null;
                        }
                    }
                    else if (current != null && double.TryParse(line.Substring(0, 8), out flow))
                    {
                        int start = 0;
                        double value = 0;


                        while (start < line.Length)
                        {
                            string token = "";

                            if (start + 8 < line.Length)
                            {
                                token = line.Substring(start, 8);
                                start += 8;
                            }
                            else
                            {
                                int length = line.Length - start;
                                token = line.Substring(start, length);
                                start += length;
                            }

                            if (double.TryParse(token, out value))
                            {
                                current.ProfileFlows.Add(tempProfiles[count], value);
                                count++;
                            }
                            else
                            {

                                break;
                            }
                        }
                    }
                }
            }
        }

        //Assign flows to cross-sections with no flows.
        List<River> tempRivers = rivers.Values.ToList();

        for (int i = 0; i < tempRivers.Count; i++)
        {
            List<Reach> reaches = tempRivers[i].Reaches.Values.ToList();

            for (int j = 0; j < reaches.Count; j++)
            {
                List<XSection> xsections = reaches[j].XSections.Values.ToList();

                for (int k = xsections.Count - 1; k >= 0; k--)
                {
                    XSection xsec = xsections[k];

                    if (xsec.ProfileFlows.Count == 0)
                    {
                        bool found = false;

                        for (int m = k; m >= 0; m--)
                        {
                            if (m != k)
                            {
                                XSection tempXsec = xsections[m];

                                if (tempXsec.ProfileFlows.Count > 0)
                                {
                                    foreach (string s in tempXsec.ProfileFlows.Keys)
                                    {
                                        xsec.ProfileFlows.Add(s, tempXsec.ProfileFlows[s]);
                                    }

                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found)
                        {
                            for (int m = k; m <= xsections.Count; m++)
                            {
                                if (m != k)
                                {
                                    XSection tempXsec = xsections[m];

                                    if (tempXsec.ProfileFlows.Count > 0)
                                    {
                                        foreach (string s in tempXsec.ProfileFlows.Keys)
                                        {
                                            xsec.ProfileFlows.Add(s, tempXsec.ProfileFlows[s]);
                                        }

                                        found = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    xsec.CreateRatingCurve();
                }
            }
        }
    }

    public void ClearProfiles()
    {
        //clear old profile data
        profiles.Clear();

        //for each of river clear
        foreach (River ri in rivers.Values)
        {
            foreach(Reach re in ri.Reaches.Values)
            re.ClearProfiles();
        }
    }

    public void WriteProfilesToExportAsGIS()
    {
        int numProfiles = 0;
        Array values = null;
        controller.Output_GetProfiles(ref numProfiles, ref values);
        List<string> lines = new List<string>();

        using (TextReader reader = new StreamReader(controller.CurrentProjectFile()))
        {
            string line = "";


            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("GIS Export Profiles"))
                {
                    lines.Add("GIS Export Profiles= " + numProfiles);

                    string lineToadd = "";
                    int count = 0;

                    while (count <= numProfiles)
                    {
                        if (count % 10 == 0)
                        {
                            if (lineToadd != "")
                            {
                                lines.Add(lineToadd);
                            }

                            lineToadd = "";
                        }

                        lineToadd += string.Format("{0,8:########}", (count + 1));
                        count++;
                    }

                    lines.Add(lineToadd);

                    break;
                }
                else
                {
                    lines.Add(line);
                }
            }
        }

        using (TextWriter writer = new StreamWriter(controller.CurrentProjectFile(), false))
        {
            foreach (string s in lines)
                writer.WriteLine(s);

            writer.Flush();
        }

    }

    public List<Point> GetPolygonForProfile(string profile, ref Reach reach)
    {
        int loc = -1;

        List<Point> polygonPoints = new List<Point>();

        List<XSection> xsections = reach.XSections.Values.ToList();

        //line along left bank
        for (int i = 0; i < reach.XSections.Count; i++)
        {
            XSection section = xsections[i];
            double elevation = section.ProfileElevations[profile];

            polygonPoints.Add(section.LeftBankPoint(elevation, out loc));
        }

        //line along right bank
        for (int i = reach.XSections.Count - 1; i >= 0; i--)
        {
            XSection section = xsections[i];
            double elevation = section.ProfileElevations[profile];

            polygonPoints.Add(section.RightBankPoint(elevation, out loc));
        }

        return polygonPoints;
    }

    public void SaveProfilesToShapeFile(FileInfo shapefile)
    {
        //Create Shapefile using .dospatial library
        using (IFeatureSet fs = new FeatureSet(FeatureType.Polygon))
        {
            //clear existing profiles
            ClearProfiles();

            //read the rivers
            ReadRivers();

            //read simulated profiles
            ReadProfiles();

            fs.Z = new double[] { };

            //add attribute fields to attribute table
            fs.DataTable.Columns.AddRange(new DataColumn[]
                        {
                          new DataColumn("RiverName" , typeof(string)),
                          new DataColumn("ReachName" , typeof(string)),
                          new DataColumn("EnsembleModel" , typeof(string)),
                          new DataColumn("ForecastDateTime" , typeof(string)),
                          new DataColumn("StartDate" , typeof(string)),
                          new DataColumn("EndDate" , typeof(string)),
                          new DataColumn("ProfileName" , typeof(string)),
                          new DataColumn("ProfileDate" , typeof(string)),
                        });


            List<River> tempRivers = rivers.Values.ToList();

            //for each profile
            for (int i = 0; i < profiles.Count; i++)
            {
                //select river
                for (int j = 0; j < rivers.Count; j++)
                {
                    string pr = profiles[i];
                    River river = tempRivers[j];

                    foreach (Reach treach in river.Reaches.Values)
                    {
                        Reach reach = treach;

                        List<Point> polygonGon = GetPolygonForProfile(pr, ref reach);

                        List<Coordinate> coordinates = new List<Coordinate>();

                        foreach (Point p in polygonGon)
                        {
                            coordinates.Add(new Coordinate(p.X, p.Y, p.Z));
                        }

                        Polygon polygon = new Polygon(coordinates);

                        IFeature f = fs.AddFeature(polygon);

                        f.DataRow.BeginEdit();


                        f.DataRow["RiverName"] = river.Name;
                        f.DataRow["ReachName"] = reach.Name;
                        f.DataRow["EnsembleModel"] = "N/A";
                        f.DataRow["StartDate"] = "N/A";
                        f.DataRow["EndDate"] = "N/A";
                        f.DataRow["ProfileName"] = pr;

                        DateTime dt;

                        if (CheckIsDate(pr, out dt))
                        {
                            f.DataRow["ProfileDate"] = dt.ToString("yyyy/MM/dd HH:mm:ss");
                        }

                        f.DataRow.EndEdit();
                    }
                }
            }

            fs.SaveAs(shapefile.FullName, true);
        }

        controller.Project_Save();
    }

    public static bool CheckIsDate(string name, out DateTime dateTime)
    {
        dateTime = new DateTime();
        string[] cols = name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

        int day, year;

        if (cols.Length == 2 && int.TryParse(cols[0].Substring(0, 2), out day) && int.TryParse(cols[0].Substring(5, 4), out year) && SDFtoShape.months.ContainsKey(cols[0].Substring(2, 3)))
        {

            dateTime = new DateTime(year, SDFtoShape.months[cols[0].Substring(2, 3)], day);
            dateTime = dateTime.AddHours(double.Parse(cols[1].Substring(0, 2)));
            dateTime = dateTime.AddMinutes(double.Parse(cols[1].Substring(2, 2)));
            return true;
        }
        else
        {
            return false;
        }
    }

    public void ReadRatingsCurves()
    {
        ClearProfiles();

        //read the rivers
        ReadRivers();

        //read simulated profiles
        ReadProfiles();

        //read steady flow data for profiles
        ReadSteadyStateFlowData();
    }

    public void OpenHECRASProjectFile()
    {
        if (File.Exists(projectFile))
        {

            controller.Project_Open("");
            controller.Project_Open(projectFile);

        }
    }

    #endregion
}
