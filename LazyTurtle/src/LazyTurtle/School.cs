using System;
using System.Drawing;

namespace LazyTurtle
{
    /// <summary>Colors of one school floor.</summary>
    sealed class FloorTheme
    {
        public Color Paint, Accent, Wood, Floor1, Floor2, Fog;
        public string NamePl, NameEn;
        public string Name => L.Pl ? NamePl : NameEn;
    }

    sealed class Subject
    {
        public string Pl, En;
        public Color Color;
        public Art.D Decor;
        public string Name => L.Pl ? Pl : En;
    }

    /// <summary>
    /// Builds the school: 8 floors, each with a lobby, a long corridor, 10 classrooms (5 on each side,
    /// one subject each) and the Master Turtle's hall at the end of the corridor with the stairs up.
    /// </summary>
    static class School
    {
        public const int Floors = 8, Classes = 10;
        public const int MapW = 54, MapH = 21;

        public static readonly Subject[] Subjects =
        {
            new Subject { Pl = "Matematyka", En = "Maths", Color = Art.C(70, 120, 210), Decor = Art.D.Abacus },
            new Subject { Pl = "Litery i słowa", En = "Letters & words", Color = Art.C(220, 80, 80), Decor = Art.D.Books },
            new Subject { Pl = "Przyroda", En = "Nature", Color = Art.C(70, 160, 70), Decor = Art.D.Aquarium },
            new Subject { Pl = "Geografia i historia", En = "Geography & history", Color = Art.C(40, 150, 170), Decor = Art.D.Globe },
            new Subject { Pl = "Angielski", En = "Polish words", Color = Art.C(150, 90, 200), Decor = Art.D.Blocks },
            new Subject { Pl = "Logika", En = "Logic", Color = Art.C(230, 140, 40), Decor = Art.D.Shapes },
            new Subject { Pl = "Czas i kalendarz", En = "Time & calendar", Color = Art.C(120, 100, 80), Decor = Art.D.Clock },
            new Subject { Pl = "Muzyka i plastyka", En = "Music & art", Color = Art.C(220, 90, 160), Decor = Art.D.Piano },
            new Subject { Pl = "Zdrowie i sport", En = "Health & sport", Color = Art.C(230, 70, 50), Decor = Art.D.Balls },
            new Subject { Pl = "Nauka i kosmos", En = "Science & space", Color = Art.C(50, 60, 140), Decor = Art.D.Telescope },
        };

        public static readonly FloorTheme[] Themes =
        {
            new FloorTheme
            {
                NamePl = "Piętro Arboretum", NameEn = "Arboretum floor",
                Paint = Art.C(255, 242, 180), Accent = Art.C(75, 185, 80), Wood = Art.C(195, 135, 78),
                Floor1 = Art.C(252, 246, 222), Floor2 = Art.C(135, 210, 105), Fog = Art.C(170, 165, 125),
            },
            new FloorTheme
            {
                NamePl = "Piętro Jeziora Kórnickiego", NameEn = "Lake Kórnik floor",
                Paint = Art.C(200, 232, 255), Accent = Art.C(45, 140, 235), Wood = Art.C(180, 125, 78),
                Floor1 = Art.C(242, 248, 255), Floor2 = Art.C(100, 175, 245), Fog = Art.C(130, 150, 175),
            },
            new FloorTheme
            {
                NamePl = "Piętro Zamkowe", NameEn = "Castle floor",
                Paint = Art.C(255, 222, 178), Accent = Art.C(245, 128, 45), Wood = Art.C(188, 118, 66),
                Floor1 = Art.C(254, 242, 222), Floor2 = Art.C(248, 165, 90), Fog = Art.C(170, 140, 112),
            },
            new FloorTheme
            {
                NamePl = "Piętro Biblioteki Kórnickiej", NameEn = "Kórnik Library floor",
                Paint = Art.C(234, 216, 255), Accent = Art.C(150, 95, 230), Wood = Art.C(170, 115, 82),
                Floor1 = Art.C(246, 240, 255), Floor2 = Art.C(180, 145, 240), Fog = Art.C(150, 135, 170),
            },
            new FloorTheme
            {
                NamePl = "Piętro Białej Damy", NameEn = "White Lady floor",
                Paint = Art.C(230, 244, 255), Accent = Art.C(95, 160, 235), Wood = Art.C(182, 165, 150),
                Floor1 = Art.C(250, 252, 255), Floor2 = Art.C(170, 205, 245), Fog = Art.C(145, 155, 178),
            },
            new FloorTheme
            {
                NamePl = "Piętro Działyńskich", NameEn = "Działyński floor",
                Paint = Art.C(255, 234, 196), Accent = Art.C(215, 55, 60), Wood = Art.C(170, 100, 58),
                Floor1 = Art.C(254, 242, 214), Floor2 = Art.C(245, 185, 65), Fog = Art.C(170, 135, 108),
            },
            new FloorTheme
            {
                NamePl = "Piętro Teofili", NameEn = "Teofila floor",
                Paint = Art.C(255, 226, 238), Accent = Art.C(235, 90, 150), Wood = Art.C(192, 128, 100),
                Floor1 = Art.C(255, 246, 238), Floor2 = Art.C(248, 165, 198), Fog = Art.C(175, 138, 150),
            },
            new FloorTheme
            {
                NamePl = "Piętro Ratusza w Bninie", NameEn = "Bnin Town Hall floor",
                Paint = Art.C(205, 246, 232), Accent = Art.C(25, 168, 150), Wood = Art.C(198, 115, 66),
                Floor1 = Art.C(246, 244, 230), Floor2 = Art.C(240, 130, 88), Fog = Art.C(120, 158, 148),
            },
        };

        /// <summary>Subject taught in classroom r on a floor (the order changes from floor to floor).</summary>
        public static int SubjectOf(int floor, int room) => (room + floor * 3) % Classes;

        public static string RoomLabel(int floor, int room) => (floor + 1) + "." + (room + 1).ToString("00");

        static readonly string[] TeacherPl = { "Pani Zofia", "Pan Leon", "Pani Halina", "Pan Bolesław", "Pani Irena", "Pan Tadeusz", "Pani Wanda", "Pan Ludwik", "Pani Krystyna", "Pan Henryk" };
        static readonly string[] TeacherEn = { "Mrs Sophie", "Mr Leon", "Mrs Hazel", "Mr Bob", "Mrs Irene", "Mr Ted", "Mrs Wanda", "Mr Louis", "Mrs Chris", "Mr Henry" };
        public static string TeacherName(int subject) => (L.Pl ? TeacherPl : TeacherEn)[subject];

        // ---- full teacher names, different for every (floor, subject); even subjects: ladies, odd: gentlemen
        static readonly string[] FirstPlF = { "Zofia", "Halina", "Irena", "Wanda", "Krystyna", "Maria", "Teresa", "Barbara", "Elżbieta", "Jadwiga",
                                              "Anna", "Ewa", "Danuta", "Grażyna", "Bożena", "Agnieszka", "Małgorzata", "Joanna", "Katarzyna", "Magdalena" };
        static readonly string[] FirstPlM = { "Leon", "Bolesław", "Tadeusz", "Ludwik", "Henryk", "Jan", "Stanisław", "Józef", "Andrzej", "Marek",
                                              "Piotr", "Tomasz", "Krzysztof", "Paweł", "Wojciech", "Michał", "Jerzy", "Zbigniew", "Adam", "Kazimierz" };
        // "feminine/masculine", or a single form used for both
        static readonly string[] SurnamePl = { "Kowalska/Kowalski", "Nowak", "Wiśniewska/Wiśniewski", "Wójcik", "Kamińska/Kamiński", "Lewandowska/Lewandowski",
                                               "Zielińska/Zieliński", "Kowalczyk", "Szymańska/Szymański", "Woźniak", "Dąbrowska/Dąbrowski", "Kozłowska/Kozłowski",
                                               "Mazur", "Jankowska/Jankowski", "Kwiatkowska/Kwiatkowski", "Krawczyk", "Piotrowska/Piotrowski", "Grabowska/Grabowski",
                                               "Zając", "Nowakowska/Nowakowski", "Pawłowska/Pawłowski", "Adamczyk", "Michalska/Michalski", "Dudek" };
        static readonly string[] SurnameEn = { "Smith", "Taylor", "Brown", "Wilson", "Evans", "Johnson", "Walker", "Wright", "Robinson", "Thompson",
                                               "White", "Hughes", "Edwards", "Green", "Hall", "Wood", "Harris", "Martin", "Jackson", "Clarke",
                                               "Lewis", "Turner", "Baker", "Cooper", "Hill", "Ward", "Morris", "King", "Moore", "Parker",
                                               "Allen", "Bennett", "Carter", "Davies", "Fisher", "Griffin", "Hunter", "Kelly", "Mason", "Price" };

        /// <summary>Full name of the teacher of a subject on a floor, e.g. "Pani Zofia Kowalska" / "Mrs Smith" (current language).</summary>
        public static string TeacherFullName(int floor, int subject)
        {
            bool lady = subject % 2 == 0;
            int idx = floor * Classes + subject;
            if (!L.Pl)
                return (lady ? "Mrs " : "Mr ") + SurnameEn[(idx * 13) % SurnameEn.Length];
            string first = (lady ? FirstPlF : FirstPlM)[(floor * 5 + subject / 2) % FirstPlF.Length];
            var forms = SurnamePl[(idx * 7) % SurnamePl.Length].Split('/');
            string surname = forms.Length == 1 || lady ? forms[0] : forms[1];
            return (lady ? "Pani " : "Pan ") + first + " " + surname;
        }

        // ---- subject artifacts (3D models with a billboard fallback) placed around the classroom edges
        enum Slot { FrontLeft, FrontRight, SideLeft, SideRight, BackLeft, BackRight, Aisle, Hang }

        sealed class Artifact
        {
            public readonly string Key;
            public readonly Slot Slot;
            public readonly int Decor;      // Art.D billboard for the software renderer, -1 = none (flat / hanging)
            public readonly double W, H;
            public Artifact(string key, Slot slot, int decor, double w, double h) { Key = key; Slot = slot; Decor = decor; W = w; H = h; }
        }

        static Artifact A(string key, Slot slot, Art.D d, double w, double h) => new Artifact(key, slot, (int)d, w, h);
        static Artifact Flat(string key, Slot slot) => new Artifact(key, slot, -1, 0.1, 0.1);

        static readonly Artifact[][] Artifacts =
        {
            new[] { A("m_solids", Slot.FrontLeft, Art.D.Shapes, 0.7, 0.7), A("m_abacus", Slot.FrontRight, Art.D.Abacus, 0.7, 0.75),
                    A("m_ruler", Slot.BackLeft, Art.D.Blocks, 0.5, 0.9) },
            new[] { A("l_inkwell", Slot.FrontLeft, Art.D.Books, 0.6, 0.7), A("l_typewriter", Slot.FrontRight, Art.D.Blocks, 0.7, 0.7),
                    A("l_bookcase", Slot.BackLeft, Art.D.Books, 1.0, 1.2) },
            new[] { A("n_aquarium", Slot.FrontLeft, Art.D.Aquarium, 1.1, 0.75), A("n_microscope", Slot.FrontRight, Art.D.Telescope, 0.6, 0.8),
                    A("n_butterflies", Slot.SideLeft, Art.D.Plant, 0.6, 0.9), A("n_plants", Slot.BackRight, Art.D.Plant, 0.6, 0.85) },
            new[] { A("g_globe", Slot.FrontLeft, Art.D.Globe, 0.6, 0.85), A("g_relief", Slot.FrontRight, Art.D.Shapes, 0.8, 0.7),
                    Flat("g_rug", Slot.Aisle), A("g_knight", Slot.BackLeft, Art.D.Blocks, 0.5, 1.1) },
            new[] { A("e_bigben", Slot.FrontLeft, Art.D.Clock, 0.5, 1.05), A("e_bus", Slot.FrontRight, Art.D.Blocks, 0.9, 0.7),
                    A("e_phonebox", Slot.BackRight, Art.D.Blocks, 0.6, 1.2) },
            new[] { A("lo_chess", Slot.FrontLeft, Art.D.Shapes, 0.8, 0.7), A("lo_cube", Slot.FrontRight, Art.D.Blocks, 0.6, 0.6),
                    A("lo_tangram", Slot.BackLeft, Art.D.Shapes, 0.7, 0.7) },
            new[] { A("t_clock", Slot.FrontLeft, Art.D.Clock, 0.5, 1.05), A("t_hourglass", Slot.FrontRight, Art.D.Clock, 0.45, 0.8),
                    A("t_sundial", Slot.BackRight, Art.D.Shapes, 0.6, 0.7) },
            new[] { A("a_piano", Slot.FrontLeft, Art.D.Piano, 1.1, 0.85), A("a_easel", Slot.FrontRight, Art.D.Easel, 0.7, 0.95),
                    A("a_drums", Slot.BackLeft, Art.D.Balls, 0.8, 0.7), A("a_guitar", Slot.SideRight, Art.D.Easel, 0.5, 0.9) },
            new[] { A("s_rack", Slot.FrontRight, Art.D.Balls, 0.8, 0.7), Flat("s_mat", Slot.Aisle),
                    A("s_hoop", Slot.BackLeft, Art.D.Balls, 0.8, 1.2) },
            new[] { A("sc_telescope", Slot.FrontLeft, Art.D.Telescope, 0.6, 0.9), A("sc_volcano", Slot.FrontRight, Art.D.Shapes, 0.7, 0.7),
                    Flat("sc_planets", Slot.Hang), A("sc_lab", Slot.BackLeft, Art.D.Blocks, 0.9, 0.75) },
        };

        /// <summary>Model keys of the artifacts in a subject's classroom.</summary>
        public static string[] ArtifactKeys(int subject) => Array.ConvertAll(Artifacts[subject], a => a.Key);

        public static World Build(int floor, Art art, int doneMask, bool masterDone)
        {
            var th = Themes[floor];
            var w = new World(MapW, MapH) { FloorNumber = floor, Theme = th };

            // ---- textures used on this floor
            byte paint = w.AddWall(Art.PaintedWall(th));
            byte bookshelf = w.AddWall(Art.Bookshelf(th, floor));
            byte notice = w.AddWall(Art.Notice(th));
            byte window = w.AddWall(Art.Window(th, floor));
            var posters = new byte[6];
            for (int i = 0; i < 6; i++) posters[i] = w.AddWall(Art.Poster(th, i));
            var lockers = new byte[3];
            for (int i = 0; i < 3; i++) lockers[i] = w.AddWall(Art.Lockers(th, 10 + i * 20 + floor * 100));
            byte royal = w.AddWall(Art.Royal());
            var royalPaint = new byte[Art.PaintingKinds];
            var wallPaint = new byte[Art.PaintingKinds];
            for (int i = 0; i < Art.PaintingKinds; i++)
            {
                royalPaint[i] = w.AddWall(Art.Painting(i, th, true));
                wallPaint[i] = w.AddWall(Art.Painting(i, th, false));
            }
            byte masterDoor = w.AddWall(Art.MasterDoor());
            w.StairsLockedTex = w.AddWall(Art.StairsUp(false));
            w.StairsOpenTex = w.AddWall(Art.StairsUp(true));
            byte yardDoor = w.AddWall(Art.YardDoor(th));
            byte displayCase = w.AddWall(Art.DisplayCase(th));
            var kornik = new byte[Art.KornikPosterKinds];
            for (int i = 0; i < Art.KornikPosterKinds; i++) kornik[i] = w.AddWall(Art.KornikPoster(th, i));
            var banner = Art.Banner(th, L.T("school_name"), L.T("school_patron"));
            var bannerIds = new byte[3];
            for (int i = 0; i < 3; i++) bannerIds[i] = w.AddWall(banner[i]);

            byte checker = w.AddFloor(Art.Checker(th));
            byte parquet = w.AddFloor(Art.Parquet());
            byte marble = w.AddFloor(Art.Marble());
            byte carpet = w.AddFloor(Art.Carpet());
            byte runner = w.AddFloor(Art.Runner(th));
            var mosaic = Art.FloorMosaic();
            var mosaicIds = new byte[9];
            for (int i = 0; i < 9; i++) mosaicIds[i] = w.AddFloor(mosaic[i]);
            w.AddCeil(Art.CeilingTile(false));
            w.AddCeil(Art.CeilingTile(true));

            // ---- carve the floor plan (everything starts as wall)
            for (int i = 0; i < w.Wall.Length; i++) w.Wall[i] = paint;
            w.Carve(1, 1, 5, 19, marble);                       // lobby
            w.Carve(1, 9, 40, 11, checker);                     // corridor
            for (int k = 0; k < 5; k++)
            {
                int x0 = 7 + 7 * k;
                w.Carve(x0, 1, x0 + 5, 7, parquet);             // top classroom
                w.Carve(x0, 13, x0 + 5, 19, parquet);           // bottom classroom
                w.Carve(x0 + 2, 8, x0 + 3, 8, parquet);         // doors
                w.Carve(x0 + 2, 12, x0 + 3, 12, parquet);
            }
            w.Carve(42, 1, 52, 19, carpet);                     // Master Turtle's hall
            for (int x = 6; x <= 40; x++) w.Floor[10 * MapW + x] = runner;     // swan carpet along the corridor
            for (int i = 0; i < 9; i++) w.Floor[(9 + i / 3) * MapW + 2 + i % 3] = mosaicIds[i];   // "SP 2 KÓRNIK" floor mosaic
            for (int y = 0; y < MapH; y++)
                for (int x = 0; x < MapW; x++)
                    w.Ceil[y * MapW + x] = (byte)((x % 3 == 1 && y % 3 == 1) ? 1 : 0);

            // ---- dress the walls (per face, so a wall looks different from the corridor and from the room)
            var rnd = new Random(floor * 31 + 7);
            var subjectPosters = new byte[Classes * 3];      // each subject poster texture is registered once per floor
            for (int k = 0; k < 5; k++)
            {
                int x0 = 7 + 7 * k;
                for (int side = 0; side < 2; side++)
                {
                    int room = side == 0 ? k : 5 + k;
                    int subject = SubjectOf(floor, room);
                    int wallRow = side == 0 ? 8 : 12, farRow = side == 0 ? 0 : 20;
                    int corridorFace = side == 0 ? World.FaceS : World.FaceN;
                    int roomFace = side == 0 ? World.FaceN : World.FaceS;
                    byte sign = w.AddWall(Art.Sign(th, RoomLabel(floor, room), Subjects[subject].Name, Subjects[subject].Color));

                    // corridor side: poster | sign | door | door | lockers | lockers
                    int kp = (room + floor * 3) % Art.KornikPosterKinds;
                    w.SetPainting(x0, wallRow, corridorFace, kornik[kp], KornikCaption + kp);
                    w.SetFace(x0 + 1, wallRow, corridorFace, sign);
                    w.SetFace(x0 + 4, wallRow, corridorFace, lockers[rnd.Next(3)]);
                    w.SetFace(x0 + 5, wallRow, corridorFace, lockers[rnd.Next(3)]);
                    // classroom: blackboard on the front wall, windows, bookshelves and posters
                    var board = Art.FloorBlackboard(th, subject, Subjects[subject].Name, floor);
                    w.SetFace(x0 + 2, farRow, roomFace == World.FaceN ? World.FaceS : World.FaceN, w.AddWall(board[0]));
                    w.SetFace(x0 + 3, farRow, roomFace == World.FaceN ? World.FaceS : World.FaceN, w.AddWall(board[1]));
                    int frontFace = side == 0 ? World.FaceS : World.FaceN;
                    w.SetFace(x0, farRow, frontFace, window);
                    w.SetFace(x0 + 1, farRow, frontFace, posters[(room + floor) % 6]);
                    w.SetFace(x0 + 4, farRow, frontFace, bookshelf);
                    w.SetFace(x0 + 5, farRow, frontFace, window);
                    // side walls: the subject's three posters (walk up to one to read its caption), shelves and windows;
                    // rows 4-5 are kept free of artifacts so the posters stay visible
                    var sp = new byte[3];
                    for (int v = 0; v < 3; v++)
                    {
                        int key = subject * 3 + v;
                        if (subjectPosters[key] == 0) subjectPosters[key] = w.AddWall(Art.SubjectPoster(th, subject, v));
                        sp[v] = subjectPosters[key];
                    }
                    int caption = SubjectCaption + subject * 3;
                    for (int y = 1; y <= 7; y++)
                    {
                        int ry = side == 0 ? y : 20 - y;
                        if (y == 4 || y == 5) w.SetPainting(x0 - 1, ry, World.FaceE, sp[y - 4], caption + y - 4);
                        else w.SetFace(x0 - 1, ry, World.FaceE, y == 2 ? bookshelf : y == 6 ? notice : paint);
                        if (y == 4) w.SetPainting(x0 + 6, ry, World.FaceW, sp[2], caption + 2);
                        else w.SetFace(x0 + 6, ry, World.FaceW, y == 2 || y == 6 ? window : paint);
                    }
                }
            }
            // lobby: banner, entrance, notices
            for (int i = 0; i < 3; i++)
            {
                w.SetFace(2 + i, 0, World.FaceS, bannerIds[i]);
            }
            // lobby south wall: Kórnik pictures and the map
            w.SetPainting(2, 20, World.FaceN, kornik[1], KornikCaption + 1);
            w.SetPainting(3, 20, World.FaceN, kornik[5], KornikCaption + 5);
            w.SetPainting(4, 20, World.FaceN, kornik[7], KornikCaption + 7);
            // the always-open door to the schoolyard (on every floor)
            w.SetFace(0, 10, World.FaceE, yardDoor);
            w.YardDoorX = 0;
            w.YardDoorY = 10;
            w.SetFace(0, 13, World.FaceE, displayCase);
            w.SetFace(0, 4, World.FaceE, window);
            w.SetFace(0, 16, World.FaceE, window);
            w.SetPainting(6, 3, World.FaceW, wallPaint[Art.PaintCastle], Art.PaintCastle);
            w.SetPainting(6, 17, World.FaceW, wallPaint[Art.PaintWhiteLady], Art.PaintWhiteLady);
            w.SetFace(6, 6, World.FaceW, notice);
            // master hall
            for (int y = 0; y < MapH; y++)
                for (int x = 41; x < MapW; x++)
                    if (w.Wall[y * MapW + x] != 0)
                    {
                        if (x == 41) w.SetFace(x, y, World.FaceE, royal);   // the west faces belong to the last classroom
                        else for (int f = 0; f < 4; f++) w.SetFace(x, y, f, royal);
                    }
            w.SetFace(41, 9, World.FaceW, displayCase);
            w.SetFace(41, 11, World.FaceW, displayCase);
            // the Działyński gallery in the Master Turtle's hall
            w.SetPainting(44, 0, World.FaceS, royalPaint[Art.PaintCastle], Art.PaintCastle);
            w.SetPainting(47, 0, World.FaceS, royalPaint[Art.PaintWhiteLady], Art.PaintWhiteLady);
            w.SetPainting(50, 0, World.FaceS, royalPaint[Art.PaintTytus], Art.PaintTytus);
            w.SetPainting(44, 20, World.FaceN, royalPaint[Art.PaintCelina], Art.PaintCelina);
            w.SetPainting(47, 20, World.FaceN, royalPaint[Art.PaintJan], Art.PaintJan);
            w.SetPainting(50, 20, World.FaceN, royalPaint[Art.PaintWhiteLady], Art.PaintWhiteLady);
            w.SetPainting(41, 4, World.FaceE, royalPaint[Art.PaintTytus], Art.PaintTytus);
            w.SetPainting(41, 16, World.FaceE, royalPaint[Art.PaintCelina], Art.PaintCelina);
            w.MasterDoorX = 41;
            w.MasterDoorY = 10;
            w.SetWall(41, 10, masterDoor);
            w.StairsX = 53;
            w.StairsY = 10;
            w.SetWall(53, 10, w.StairsLockedTex);
            w.MasterDoorFloor = carpet;

            // ---- people and furniture
            for (int k = 0; k < 5; k++)
                for (int side = 0; side < 2; side++)
                    BuildClassroom(w, art, floor, k, side, (doneMask & (1 << (side == 0 ? k : 5 + k))) != 0, rnd);

            var master = w.Add(Kind.Master, 48.5, 10.5, 0, 1.0, 1.25, art.Master, 0);
            master.HalfX = master.HalfY = 0.35;
            master.Model = "master";
            master.Yaw = Math.PI;
            w.Master = master;
            w.Add(Kind.Decor, 50.2, 10.5, 0, 1.2, 1.5, art.Decor[(int)Art.D.Throne], 0).HalfX = 0.5;
            w.MasterMarker = w.Add(Kind.Marker, 48.5, 10.5, 1.32, 0.3, 0.3, art.MarkerAsk, 0);
            w.MasterMarker.Bob = true;
            foreach (var p in new[] { new PointF(43, 2), new PointF(52, 2), new PointF(43, 19), new PointF(52, 19) })
                Plant(w, art, p.X, p.Y, 0.6, 0.85);
            var globe = w.Add(Kind.Decor, 51.5, 5.5, 0, 0.6, 0.85, art.Decor[(int)Art.D.Globe], 0);
            globe.Radius = 0.25;
            globe.Model = "globe";
            w.Add(Kind.Decor, 51.5, 15.5, 0, 0.5, 0.95, art.Decor[(int)Art.D.Clock], 0).Radius = 0.25;

            // lobby
            Plant(w, art, 1.6, 1.6, 0.6, 0.85);
            Plant(w, art, 5.4, 1.6, 0.6, 0.85);
            Plant(w, art, 1.6, 19.4, 0.6, 0.85);
            w.Add(Kind.Decor, 1.5, 6.5, 0, 0.55, 0.8, art.Decor[(int)Art.D.Fountain], 0).Radius = 0.25;
            Bench(w, art, 3.5, 1.4, 1.0, Math.PI / 2);
            Bench(w, art, 3.5, 19.6, 1.0, -Math.PI / 2);
            // corridor benches and plants by the dividing walls
            for (int k = 0; k < 4; k++)
            {
                Plant(w, art, 13.5 + 7 * k, 9.25, 0.5, 0.7);
                Bench(w, art, 13.5 + 7 * k, 11.75, 0.9, -Math.PI / 2);
            }
            // coins (some floating: jump for them!)
            for (int x = 8; x <= 38; x += 3)
                w.AddCoin(art, x + 0.5, 10.5, x % 2 == 0 ? 0.5 : 0.08);
            for (int y = 3; y <= 17; y += 2)
                w.AddCoin(art, 3.5, y + 0.5, y % 4 == 1 ? 0.5 : 0.08);
            // classmates walking around the corridor
            for (int i = 0; i < 4; i++)
            {
                var kid = w.Add(Kind.Kid, 6 + i * 9.3, i % 2 == 0 ? 9.6 : 11.4, 0, 0.5, 0.58, art.Kid[i % 3], 4);
                kid.Dir = i % 2 == 0 ? 1 : -1;
                kid.Variant = i % 3;
                kid.Model = "kid" + kid.Variant;
                kid.Yaw = kid.Dir > 0 ? 0 : Math.PI;
                kid.Name = Chat.KidName(1 + floor * 4 + i);
            }

            w.StartX = 3;
            w.StartY = 10.5;
            w.StartAngle = 0;
            if (masterDone) w.OpenStairs();
            if (w.GiftsDone == Classes) w.OpenMasterDoor();
            return w;
        }

        static void BuildClassroom(World w, Art art, int floor, int k, int side, bool done, Random rnd)
        {
            int room = side == 0 ? k : 5 + k;
            int subject = SubjectOf(floor, room);
            double x0 = 7 + 7 * k;
            Func<double, double> Y = y => side == 0 ? y : 21 - y;   // mirror for the rooms below the corridor

            var r = new Room { Index = room, Subject = subject, Label = RoomLabel(floor, room), Done = done, TeacherName = TeacherFullName(floor, subject) };
            r.X0 = (int)x0;
            r.X1 = (int)x0 + 5;
            r.Y0 = side == 0 ? 1 : 13;
            r.Y1 = side == 0 ? 7 : 19;
            double cx = x0 + 3;

            double facing = side == 0 ? Math.PI / 2 : -Math.PI / 2;    // teachers look at the class (away from the blackboard)
            r.Teacher = w.Add(Kind.Teacher, cx, Y(1.65), 0, 0.85, 1.05, art.Teacher[subject], 0);
            r.Teacher.Model = "teacher" + subject;
            r.Teacher.Yaw = facing;
            var desk = w.Add(Kind.Decor, cx, Y(2.55), 0, 1.6, 0.62, art.TeacherDesk, 0);
            desk.HalfX = 0.8;
            desk.HalfY = 0.3;
            desk.Model = "tdesk";
            desk.Yaw = facing;
            r.Gift = w.Add(Kind.Gift, cx + 0.45, Y(2.5), 0.6, 0.32, 0.32, art.GiftBox, 0);
            r.Gift.Model = "gift";
            r.Gift.Bob = true;
            r.Gift.Alive = !done;
            r.Marker = w.Add(Kind.Marker, cx, Y(1.65), 1.12, 0.28, 0.28, done ? art.MarkerDone : art.MarkerAsk, 0);
            r.Marker.Bob = true;

            // pupils' desks in two columns, aisle in the middle (from the door to the teacher)
            for (int row = 0; row < 2; row++)
                foreach (var dx in new[] { 1.0, 5.0 })
                {
                    int v = rnd.Next(4);
                    var d = w.Add(Kind.Decor, x0 + dx, Y(4.4 + row * 1.6), 0, 0.95, 0.9, art.StudentDesk[v], 0);
                    d.HalfX = 0.45;
                    d.HalfY = 0.3;
                    d.Model = "desk" + v;
                    d.Yaw = -facing;            // pupils look at the blackboard
                }
            // the subject's artifacts around the room edges (the aisle from the door to the teacher stays free)
            foreach (var a in Artifacts[subject])
                PlaceArtifact(w, art, a, x0, side, Y);
            w.Rooms.Add(r);
        }

        /// <summary>
        /// Places one artifact. Positions are in the coordinates of a top-row classroom (blackboard wall at y = 1, door at y = 8,
        /// pupils' desks at x0+1 / x0+5 and y 4.4 / 6.0, teacher's desk x0+2.2..x0+3.8 / y 2.25..2.85); Y mirrors them below the corridor.
        /// </summary>
        static void PlaceArtifact(World w, Art art, Artifact a, double x0, int side, Func<double, double> Y)
        {
            const double cx = 3.0, cy = 4.8;    // room centre the corner pieces turn to
            double x, y, yaw, scale = 1, radius = a.W >= 1.0 ? 0.45 : 0.35;
            switch (a.Slot)
            {
                case Slot.FrontLeft: x = 0.65; y = 1.6; break;
                case Slot.FrontRight: x = 5.35; y = 1.6; break;
                case Slot.BackLeft: x = 0.65; y = 7.35; break;
                case Slot.BackRight: x = 5.35; y = 7.35; break;
                case Slot.SideLeft: x = 0.45; y = 3.3; scale = 0.85; radius = 0.3; break;
                case Slot.SideRight: x = 5.55; y = 3.3; scale = 0.85; radius = 0.3; break;
                case Slot.Aisle: x = 3.0; y = 5.2; radius = 0; break;      // flat on the floor: walk over it
                default: x = 3.0; y = 4.8; radius = 0; break;              // hanging high above the aisle
            }
            yaw = a.Slot == Slot.SideLeft ? 0 : a.Slot == Slot.SideRight ? Math.PI
                : a.Slot == Slot.Aisle || a.Slot == Slot.Hang ? Math.PI / 2 : Math.Atan2(cy - y, cx - x);
            if (side != 0) yaw = -yaw;          // mirrored room below the corridor
            var e = w.Add(Kind.Decor, x0 + x, Y(y), 0, a.W * scale, a.H * scale, a.Decor >= 0 ? art.Decor[a.Decor] : null, 2);
            e.Model = a.Key;
            e.Yaw = yaw;
            e.Scale = scale;
            e.Radius = radius;
        }

        /// <summary>Caption ids >= this are Kórnik posters (Art.KornikPosterCaption), below are paintings (Lang "paint{n}").</summary>
        public const int KornikCaption = 100;

        /// <summary>Caption ids >= this are subject posters: SubjectCaption + subject * 3 + variant (Art.SubjectPosterCaption).</summary>
        public const int SubjectCaption = 200;

        static void Plant(World w, Art art, double x, double y, double wd, double h)
        {
            var e = w.Add(Kind.Decor, x, y, 0, wd, h, art.Decor[(int)Art.D.Plant], 0);
            e.Radius = 0.22;
            e.Model = "plant";
            e.Scale = h / 0.9;
        }

        static void Bench(World w, Art art, double x, double y, double length, double yaw)
        {
            var e = w.Add(Kind.Decor, x, y, 0, length, 0.5, art.Decor[(int)Art.D.Bench], 0);
            e.HalfX = 0.45 * length;
            e.HalfY = 0.25;
            e.Model = "bench";
            e.Yaw = yaw;
            e.Scale = length;
        }
    }
}
