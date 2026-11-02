// Port of School.cs (floor plan, rooms, people and furniture) with the wall dressing of World.cs
// and the caption texts of Lang.cs / ArtKornik.cs / ArtSubject.cs.

#include "LTSchool.h"
#include "Math/RandomStream.h"
#include "Math/Color.h"

namespace LTSchoolDetail
{
	/** Art.C: an sRGB colour from the original palette, converted to linear. */
	FLinearColor SRGB(int32 R, int32 G, int32 B)
	{
		return FLinearColor(FColor((uint8)FMath::Clamp(R, 0, 255), (uint8)FMath::Clamp(G, 0, 255), (uint8)FMath::Clamp(B, 0, 255)));
	}

	// Painting kinds (Art.cs) = caption ids 0-4.
	constexpr int32 PaintWhiteLady = 0, PaintTytus = 1, PaintCelina = 2, PaintJan = 3, PaintCastle = 4;
	constexpr int32 KornikPosterKinds = 8;

	const TCHAR* KidNamesPl[] = { TEXT("Kuba"), TEXT("Ola"), TEXT("Staś"), TEXT("Zosia"), TEXT("Antek"), TEXT("Hania"), TEXT("Franek"), TEXT("Lena"), TEXT("Tymek"), TEXT("Maja"), TEXT("Jaś"), TEXT("Pola") };
	const TCHAR* KidNamesEn[] = { TEXT("Jack"), TEXT("Olivia"), TEXT("Sam"), TEXT("Sophie"), TEXT("Tony"), TEXT("Hannah"), TEXT("Frank"), TEXT("Lena"), TEXT("Tim"), TEXT("Maya"), TEXT("Johnny"), TEXT("Polly") };

	/** Chat.KidName: first name of classmate number Index (wraps around). */
	FString KidName(int32 Index, bool bPolish)
	{
		const int32 N = (int32)UE_ARRAY_COUNT(KidNamesPl);
		const int32 I = ((Index % N) + N) % N;
		return bPolish ? KidNamesPl[I] : KidNamesEn[I];
	}

	// ---- teacher names (School.cs)
	const TCHAR* FirstPlF[] = { TEXT("Zofia"), TEXT("Halina"), TEXT("Irena"), TEXT("Wanda"), TEXT("Krystyna"), TEXT("Maria"), TEXT("Teresa"), TEXT("Barbara"), TEXT("Elżbieta"), TEXT("Jadwiga"),
	                            TEXT("Anna"), TEXT("Ewa"), TEXT("Danuta"), TEXT("Grażyna"), TEXT("Bożena"), TEXT("Agnieszka"), TEXT("Małgorzata"), TEXT("Joanna"), TEXT("Katarzyna"), TEXT("Magdalena") };
	const TCHAR* FirstPlM[] = { TEXT("Leon"), TEXT("Bolesław"), TEXT("Tadeusz"), TEXT("Ludwik"), TEXT("Henryk"), TEXT("Jan"), TEXT("Stanisław"), TEXT("Józef"), TEXT("Andrzej"), TEXT("Marek"),
	                            TEXT("Piotr"), TEXT("Tomasz"), TEXT("Krzysztof"), TEXT("Paweł"), TEXT("Wojciech"), TEXT("Michał"), TEXT("Jerzy"), TEXT("Zbigniew"), TEXT("Adam"), TEXT("Kazimierz") };
	// "feminine/masculine", or a single form used for both
	const TCHAR* SurnamePl[] = { TEXT("Kowalska/Kowalski"), TEXT("Nowak"), TEXT("Wiśniewska/Wiśniewski"), TEXT("Wójcik"), TEXT("Kamińska/Kamiński"), TEXT("Lewandowska/Lewandowski"),
	                             TEXT("Zielińska/Zieliński"), TEXT("Kowalczyk"), TEXT("Szymańska/Szymański"), TEXT("Woźniak"), TEXT("Dąbrowska/Dąbrowski"), TEXT("Kozłowska/Kozłowski"),
	                             TEXT("Mazur"), TEXT("Jankowska/Jankowski"), TEXT("Kwiatkowska/Kwiatkowski"), TEXT("Krawczyk"), TEXT("Piotrowska/Piotrowski"), TEXT("Grabowska/Grabowski"),
	                             TEXT("Zając"), TEXT("Nowakowska/Nowakowski"), TEXT("Pawłowska/Pawłowski"), TEXT("Adamczyk"), TEXT("Michalska/Michalski"), TEXT("Dudek") };
	const TCHAR* SurnameEn[] = { TEXT("Smith"), TEXT("Taylor"), TEXT("Brown"), TEXT("Wilson"), TEXT("Evans"), TEXT("Johnson"), TEXT("Walker"), TEXT("Wright"), TEXT("Robinson"), TEXT("Thompson"),
	                             TEXT("White"), TEXT("Hughes"), TEXT("Edwards"), TEXT("Green"), TEXT("Hall"), TEXT("Wood"), TEXT("Harris"), TEXT("Martin"), TEXT("Jackson"), TEXT("Clarke"),
	                             TEXT("Lewis"), TEXT("Turner"), TEXT("Baker"), TEXT("Cooper"), TEXT("Hill"), TEXT("Ward"), TEXT("Morris"), TEXT("King"), TEXT("Moore"), TEXT("Parker"),
	                             TEXT("Allen"), TEXT("Bennett"), TEXT("Carter"), TEXT("Davies"), TEXT("Fisher"), TEXT("Griffin"), TEXT("Hunter"), TEXT("Kelly"), TEXT("Mason"), TEXT("Price") };

	// ---- subject artifacts placed around the classroom edges
	enum class ESlot : uint8 { FrontLeft, FrontRight, SideLeft, SideRight, BackLeft, BackRight, Aisle, Hang };

	struct FArtifact
	{
		const TCHAR* Key;
		ESlot Slot;
		double W, H;
	};

	// Flat artifacts (rug, mat, hanging planets) use 0.1 x 0.1 as in the original.
	const TArray<TArray<FArtifact>>& Artifacts()
	{
		static const TArray<TArray<FArtifact>> Table =
		{
			{ { TEXT("m_solids"), ESlot::FrontLeft, 0.7, 0.7 }, { TEXT("m_abacus"), ESlot::FrontRight, 0.7, 0.75 }, { TEXT("m_ruler"), ESlot::BackLeft, 0.5, 0.9 } },
			{ { TEXT("l_inkwell"), ESlot::FrontLeft, 0.6, 0.7 }, { TEXT("l_typewriter"), ESlot::FrontRight, 0.7, 0.7 }, { TEXT("l_bookcase"), ESlot::BackLeft, 1.0, 1.2 } },
			{ { TEXT("n_aquarium"), ESlot::FrontLeft, 1.1, 0.75 }, { TEXT("n_microscope"), ESlot::FrontRight, 0.6, 0.8 },
			  { TEXT("n_butterflies"), ESlot::SideLeft, 0.6, 0.9 }, { TEXT("n_plants"), ESlot::BackRight, 0.6, 0.85 } },
			{ { TEXT("g_globe"), ESlot::FrontLeft, 0.6, 0.85 }, { TEXT("g_relief"), ESlot::FrontRight, 0.8, 0.7 },
			  { TEXT("g_rug"), ESlot::Aisle, 0.1, 0.1 }, { TEXT("g_knight"), ESlot::BackLeft, 0.5, 1.1 } },
			{ { TEXT("e_bigben"), ESlot::FrontLeft, 0.5, 1.05 }, { TEXT("e_bus"), ESlot::FrontRight, 0.9, 0.7 }, { TEXT("e_phonebox"), ESlot::BackRight, 0.6, 1.2 } },
			{ { TEXT("lo_chess"), ESlot::FrontLeft, 0.8, 0.7 }, { TEXT("lo_cube"), ESlot::FrontRight, 0.6, 0.6 }, { TEXT("lo_tangram"), ESlot::BackLeft, 0.7, 0.7 } },
			{ { TEXT("t_clock"), ESlot::FrontLeft, 0.5, 1.05 }, { TEXT("t_hourglass"), ESlot::FrontRight, 0.45, 0.8 }, { TEXT("t_sundial"), ESlot::BackRight, 0.6, 0.7 } },
			{ { TEXT("a_piano"), ESlot::FrontLeft, 1.1, 0.85 }, { TEXT("a_easel"), ESlot::FrontRight, 0.7, 0.95 },
			  { TEXT("a_drums"), ESlot::BackLeft, 0.8, 0.7 }, { TEXT("a_guitar"), ESlot::SideRight, 0.5, 0.9 } },
			{ { TEXT("s_rack"), ESlot::FrontRight, 0.8, 0.7 }, { TEXT("s_mat"), ESlot::Aisle, 0.1, 0.1 }, { TEXT("s_hoop"), ESlot::BackLeft, 0.8, 1.2 } },
			{ { TEXT("sc_telescope"), ESlot::FrontLeft, 0.6, 0.9 }, { TEXT("sc_volcano"), ESlot::FrontRight, 0.7, 0.7 },
			  { TEXT("sc_planets"), ESlot::Hang, 0.1, 0.1 }, { TEXT("sc_lab"), ESlot::BackLeft, 0.9, 0.75 } },
		};
		return Table;
	}

	/** Neighbour cell a wall face looks at. */
	FIntPoint FaceNeighbour(int32 X, int32 Y, ELTFace Face)
	{
		switch (Face)
		{
		case ELTFace::N: return FIntPoint(X, Y - 1);
		case ELTFace::S: return FIntPoint(X, Y + 1);
		case ELTFace::W: return FIntPoint(X - 1, Y);
		default: return FIntPoint(X + 1, Y);
		}
	}

	/** The original World's carving and per-face texture overrides, recorded as cells, surfaces and decorations. */
	struct FBuilder
	{
		FLTFloorLayout& L;

		explicit FBuilder(FLTFloorLayout& InLayout) : L(InLayout) {}

		void Carve(int32 X0, int32 Y0, int32 X1, int32 Y1, ELTCell Cell, ELTSurface Surface)
		{
			for (int32 Y = Y0; Y <= Y1; Y++)
				for (int32 X = X0; X <= X1; X++)
				{
					L.Cells[Y * LT::MapW + X] = Cell;
					L.Surfaces[Y * LT::MapW + X] = Surface;
				}
		}

		/** Back to the plain painted wall (World.SetFace with the paint texture). */
		void ClearFace(int32 X, int32 Y, ELTFace Face)
		{
			L.Decor.RemoveAll([X, Y, Face](const FLTWallDecor& D) { return D.X == X && D.Y == Y && D.Face == Face; });
		}

		/** World.SetWall: drops every face override of a cell. */
		void ClearCell(int32 X, int32 Y)
		{
			L.Decor.RemoveAll([X, Y](const FLTWallDecor& D) { return D.X == X && D.Y == Y; });
		}

		/** World.SetFace: replaces whatever hangs on that face. */
		FLTWallDecor* SetFace(int32 X, int32 Y, ELTFace Face, ELTWallDecor Kind, int32 Index = 0, int32 Room = -1)
		{
			if (!L.InBounds(X, Y)) return nullptr;
			ClearFace(X, Y, Face);
			FLTWallDecor& D = L.Decor.AddDefaulted_GetRef();
			D.X = X;
			D.Y = Y;
			D.Face = Face;
			D.Kind = Kind;
			D.Index = Index;
			D.Room = Room;
			return &D;
		}

		/** World.SetPainting: hangs a picture and remembers the spot in front of it where its caption is shown. */
		FLTWallDecor* SetPainting(int32 X, int32 Y, ELTFace Face, ELTWallDecor Kind, int32 Index, int32 Caption, int32 Room = -1)
		{
			FLTWallDecor* D = SetFace(X, Y, Face, Kind, Index, Room);
			if (D) D->Caption = Caption;
			double PX = X + 0.5, PY = Y + 0.5;
			switch (Face)
			{
			case ELTFace::N: PY -= 1; break;
			case ELTFace::S: PY += 1; break;
			case ELTFace::W: PX -= 1; break;
			default: PX += 1; break;
			}
			FLTCaptionSpot& Spot = L.Captions.AddDefaulted_GetRef();
			Spot.X = PX;
			Spot.Y = PY;
			Spot.Caption = Caption;
			return D;
		}

		FLTPropSpawn& Add(ELTProp Kind, const TCHAR* Key, double X, double Y, double Z, double W, double H)
		{
			FLTPropSpawn& P = L.Props.AddDefaulted_GetRef();
			P.Kind = Kind;
			P.Key = FName(Key);
			P.X = X;
			P.Y = Y;
			P.Z = Z;
			P.W = W;
			P.H = H;
			return P;
		}

		/**
		 * The original collided with boxes aligned to the map axes; the layout stores them in the prop's local frame,
		 * so the half sizes swap for props turned sideways. Call after setting Yaw.
		 */
		static void SetBox(FLTPropSpawn& P, double MapHalfX, double MapHalfY)
		{
			const bool bSideways = FMath::Abs(FMath::Sin(P.Yaw)) > 0.7;
			P.HalfX = bSideways ? MapHalfY : MapHalfX;
			P.HalfY = bSideways ? MapHalfX : MapHalfY;
		}

		void Plant(double X, double Y, double W, double H)
		{
			FLTPropSpawn& P = Add(ELTProp::Plant, TEXT("plant"), X, Y, 0, W, H);
			P.Radius = 0.22;
			P.Scale = H / 0.9;
		}

		void Bench(double X, double Y, double Length, double Yaw)
		{
			FLTPropSpawn& P = Add(ELTProp::Bench, TEXT("bench"), X, Y, 0, Length, 0.5);
			P.Yaw = Yaw;
			P.Scale = Length;
			SetBox(P, 0.45 * Length, 0.25);
		}

		void Coin(double X, double Y, double Z)
		{
			Add(ELTProp::Coin, TEXT("coin"), X, Y, Z, 0.28, 0.28);
		}
	};

	/**
	 * Places one artifact. Positions are in the coordinates of a top-row classroom (blackboard wall at y = 1, door at y = 8,
	 * pupils' desks at x0+1 / x0+5 and y 4.4 / 6.0, teacher's desk x0+2.2..x0+3.8 / y 2.25..2.85); mirrored below the corridor.
	 */
	void PlaceArtifact(FBuilder& B, const FArtifact& A, double X0, int32 Side, int32 Room)
	{
		const double CX = 3.0, CY = 4.8;    // room centre the corner pieces turn to
		double X, Y, Scale = 1, Radius = A.W >= 1.0 ? 0.45 : 0.35;
		switch (A.Slot)
		{
		case ESlot::FrontLeft: X = 0.65; Y = 1.6; break;
		case ESlot::FrontRight: X = 5.35; Y = 1.6; break;
		case ESlot::BackLeft: X = 0.65; Y = 7.35; break;
		case ESlot::BackRight: X = 5.35; Y = 7.35; break;
		case ESlot::SideLeft: X = 0.45; Y = 3.3; Scale = 0.85; Radius = 0.3; break;
		case ESlot::SideRight: X = 5.55; Y = 3.3; Scale = 0.85; Radius = 0.3; break;
		case ESlot::Aisle: X = 3.0; Y = 5.2; Radius = 0; break;     // flat on the floor: walk over it
		default: X = 3.0; Y = 4.8; Radius = 0; break;               // hanging high above the aisle
		}
		double Yaw = A.Slot == ESlot::SideLeft ? 0
			: A.Slot == ESlot::SideRight ? UE_DOUBLE_PI
			: (A.Slot == ESlot::Aisle || A.Slot == ESlot::Hang) ? UE_DOUBLE_PI / 2
			: FMath::Atan2(CY - Y, CX - X);
		if (Side != 0) Yaw = -Yaw;          // mirrored room below the corridor
		FLTPropSpawn& P = B.Add(ELTProp::Artifact, A.Key, X0 + X, Side == 0 ? Y : 21 - Y, 0, A.W * Scale, A.H * Scale);
		P.Yaw = Yaw;
		P.Scale = Scale;
		P.Radius = Radius;
		P.Room = Room;
	}

	void BuildClassroom(FBuilder& B, int32 Floor, int32 K, int32 Side, bool bPolish, FRandomStream& Rnd)
	{
		const int32 Room = Side == 0 ? K : 5 + K;
		const int32 Subject = FLTSchool::SubjectOf(Floor, Room);
		const double X0 = 7 + 7 * K;
		auto Y = [Side](double V) { return Side == 0 ? V : 21 - V; };   // mirror for the rooms below the corridor

		FLTRoomLayout& R = B.L.Rooms[Room];
		R.Index = Room;
		R.Subject = Subject;
		R.Label = FLTSchool::RoomLabel(Floor, Room);
		R.TeacherName = FLTSchool::TeacherFullName(Floor, Subject, bPolish);
		R.X0 = (int32)X0;
		R.X1 = (int32)X0 + 5;
		R.Y0 = Side == 0 ? 1 : 13;
		R.Y1 = Side == 0 ? 7 : 19;
		const double CX = X0 + 3;

		const double Facing = Side == 0 ? UE_DOUBLE_PI / 2 : -UE_DOUBLE_PI / 2;    // teachers look at the class (away from the blackboard)
		FLTPropSpawn& Teacher = B.Add(ELTProp::Teacher, *FString::Printf(TEXT("teacher%d"), Subject), CX, Y(1.65), 0, 0.85, 1.05);
		Teacher.Yaw = Facing;
		Teacher.Room = Room;
		Teacher.Variant = Subject;
		Teacher.Name = R.TeacherName;
		R.TeacherPos = FVector2D(CX, Y(1.65));

		FLTPropSpawn& Desk = B.Add(ELTProp::TeacherDesk, TEXT("tdesk"), CX, Y(2.55), 0, 1.6, 0.62);
		Desk.Yaw = Facing;
		Desk.Room = Room;
		FBuilder::SetBox(Desk, 0.8, 0.3);

		FLTPropSpawn& Gift = B.Add(ELTProp::Gift, TEXT("gift"), CX + 0.45, Y(2.5), 0.6, 0.32, 0.32);
		Gift.Room = Room;
		Gift.Variant = Subject;
		R.GiftPos = FVector2D(CX + 0.45, Y(2.5));

		FLTPropSpawn& Marker = B.Add(ELTProp::Marker, TEXT("marker"), CX, Y(1.65), 1.12, 0.28, 0.28);
		Marker.Room = Room;

		// pupils' desks in two columns, aisle in the middle (from the door to the teacher)
		for (int32 Row = 0; Row < 2; Row++)
			for (const double DX : { 1.0, 5.0 })
			{
				const int32 V = Rnd.RandRange(0, 3);
				FLTPropSpawn& D = B.Add(ELTProp::StudentDesk, *FString::Printf(TEXT("desk%d"), V), X0 + DX, Y(4.4 + Row * 1.6), 0, 0.95, 0.9);
				D.Yaw = -Facing;            // pupils look at the blackboard
				D.Variant = V;
				D.Room = Room;
				FBuilder::SetBox(D, 0.45, 0.3);
			}
		// the subject's artifacts around the room edges (the aisle from the door to the teacher stays free)
		for (const FArtifact& A : Artifacts()[Subject])
			PlaceArtifact(B, A, X0, Side, Room);
	}
}

using namespace LTSchoolDetail;

const TArray<FLTSubject>& FLTSchool::Subjects()
{
	static const TArray<FLTSubject> List =
	{
		{ TEXT("Matematyka"), TEXT("Maths"), SRGB(70, 120, 210) },
		{ TEXT("Litery i słowa"), TEXT("Letters & words"), SRGB(220, 80, 80) },
		{ TEXT("Przyroda"), TEXT("Nature"), SRGB(70, 160, 70) },
		{ TEXT("Geografia i historia"), TEXT("Geography & history"), SRGB(40, 150, 170) },
		{ TEXT("Angielski"), TEXT("Polish words"), SRGB(150, 90, 200) },
		{ TEXT("Logika"), TEXT("Logic"), SRGB(230, 140, 40) },
		{ TEXT("Czas i kalendarz"), TEXT("Time & calendar"), SRGB(120, 100, 80) },
		{ TEXT("Muzyka i plastyka"), TEXT("Music & art"), SRGB(220, 90, 160) },
		{ TEXT("Zdrowie i sport"), TEXT("Health & sport"), SRGB(230, 70, 50) },
		{ TEXT("Nauka i kosmos"), TEXT("Science & space"), SRGB(50, 60, 140) },
	};
	return List;
}

const TArray<FLTFloorTheme>& FLTSchool::Themes()
{
	// NamePl, NameEn, Paint, Accent, Wood, Floor1, Floor2, Fog
	static const TArray<FLTFloorTheme> List =
	{
		{ TEXT("Piętro Arboretum"), TEXT("Arboretum floor"),
		  SRGB(250, 238, 190), SRGB(90, 170, 90), SRGB(165, 115, 70), SRGB(235, 228, 205), SRGB(130, 185, 115), SRGB(70, 62, 55) },
		{ TEXT("Piętro Jeziora Kórnickiego"), TEXT("Lake Kórnik floor"),
		  SRGB(205, 228, 250), SRGB(70, 125, 210), SRGB(150, 105, 70), SRGB(230, 232, 240), SRGB(110, 150, 215), SRGB(55, 60, 75) },
		{ TEXT("Piętro Zamkowe"), TEXT("Castle floor"),
		  SRGB(255, 220, 185), SRGB(230, 125, 60), SRGB(160, 100, 60), SRGB(240, 230, 215), SRGB(225, 150, 95), SRGB(75, 58, 48) },
		{ TEXT("Piętro Biblioteki Kórnickiej"), TEXT("Kórnik Library floor"),
		  SRGB(228, 210, 245), SRGB(140, 95, 200), SRGB(140, 95, 75), SRGB(235, 230, 240), SRGB(160, 125, 210), SRGB(62, 52, 72) },
		{ TEXT("Piętro Białej Damy"), TEXT("White Lady floor"),
		  SRGB(236, 241, 249), SRGB(120, 150, 200), SRGB(150, 140, 135), SRGB(242, 244, 248), SRGB(172, 188, 218), SRGB(58, 62, 74) },
		{ TEXT("Piętro Działyńskich"), TEXT("Działyński floor"),
		  SRGB(250, 228, 200), SRGB(175, 40, 45), SRGB(130, 72, 45), SRGB(242, 228, 202), SRGB(205, 155, 60), SRGB(70, 45, 40) },
		{ TEXT("Piętro Teofili"), TEXT("Teofila floor"),
		  SRGB(252, 226, 230), SRGB(210, 100, 130), SRGB(165, 112, 92), SRGB(250, 240, 226), SRGB(232, 168, 178), SRGB(72, 55, 60) },
		{ TEXT("Piętro Ratusza w Bninie"), TEXT("Bnin Town Hall floor"),
		  SRGB(212, 238, 232), SRGB(40, 140, 135), SRGB(170, 95, 60), SRGB(236, 233, 222), SRGB(200, 112, 78), SRGB(45, 64, 64) },
	};
	return List;
}

FString FLTSchool::RoomLabel(int32 Floor, int32 Room)
{
	return FString::Printf(TEXT("%d.%02d"), Floor + 1, Room + 1);
}

FString FLTSchool::TeacherFullName(int32 Floor, int32 Subject, bool bPolish)
{
	const bool bLady = Subject % 2 == 0;
	const int32 Idx = FMath::Max(0, Floor * LT::Classes + Subject);
	if (!bPolish)
		return FString(bLady ? TEXT("Mrs ") : TEXT("Mr ")) + SurnameEn[(Idx * 13) % (int32)UE_ARRAY_COUNT(SurnameEn)];
	const int32 FirstIdx = FMath::Max(0, Floor * 5 + Subject / 2) % (int32)UE_ARRAY_COUNT(FirstPlF);
	const FString First = bLady ? FirstPlF[FirstIdx] : FirstPlM[FirstIdx];
	TArray<FString> Forms;
	FString(SurnamePl[(Idx * 7) % (int32)UE_ARRAY_COUNT(SurnamePl)]).ParseIntoArray(Forms, TEXT("/"));
	const FString Surname = Forms.Num() == 1 || bLady ? Forms[0] : Forms[1];
	return FString(bLady ? TEXT("Pani ") : TEXT("Pan ")) + First + TEXT(" ") + Surname;
}

TArray<FName> FLTSchool::ArtifactKeys(int32 Subject)
{
	TArray<FName> Keys;
	if (Artifacts().IsValidIndex(Subject))
		for (const FArtifact& A : Artifacts()[Subject])
			Keys.Add(FName(A.Key));
	return Keys;
}

FString FLTSchool::CaptionTitle(int32 Caption, bool bPolish)
{
	auto SL = [bPolish](const TCHAR* Pl, const TCHAR* En) { return FString(bPolish ? Pl : En); };
	if (Caption >= SubjectCaption && Caption < SubjectCaption + LT::Classes * 3)
	{
		// ArtSubject.SPosterTitle
		switch (Caption - SubjectCaption)
		{
		case 0: return SL(TEXT("Tabliczka mnożenia"), TEXT("Times table"));
		case 1: return SL(TEXT("Figury"), TEXT("Shapes"));
		case 2: return SL(TEXT("Ułamki"), TEXT("Fractions"));
		case 3: return SL(TEXT("Alfabet"), TEXT("Alphabet"));
		case 4: return SL(TEXT("Części mowy"), TEXT("Parts of speech"));
		case 5: return SL(TEXT("Ortografia"), TEXT("Spelling"));
		case 6: return SL(TEXT("Łańcuch pokarmowy"), TEXT("Food chain"));
		case 7: return SL(TEXT("Budowa rośliny"), TEXT("Parts of a plant"));
		case 8: return SL(TEXT("Rozwój motyla"), TEXT("Butterfly life cycle"));
		case 9: return SL(TEXT("Mapa Polski"), TEXT("Map of Poland"));
		case 10: return SL(TEXT("Historia Polski"), TEXT("Polish history"));
		case 11: return SL(TEXT("Kontynenty"), TEXT("Continents"));
		case 12: return SL(TEXT("Hello! Cześć!"), TEXT("Cześć! Hello!"));
		case 13: return SL(TEXT("Colours & numbers"), TEXT("Kolory i liczby"));
		case 14: return SL(TEXT("London"), TEXT("Londyn"));
		case 15: return SL(TEXT("Szachy"), TEXT("Chess"));
		case 16: return SL(TEXT("Co dalej?"), TEXT("What comes next?"));
		case 17: return TEXT("Tangram");
		case 18: return SL(TEXT("Która godzina?"), TEXT("What time is it?"));
		case 19: return SL(TEXT("Pory roku"), TEXT("Seasons & months"));
		case 20: return SL(TEXT("Pory dnia"), TEXT("Times of day"));
		case 21: return SL(TEXT("Nuty"), TEXT("Musical notes"));
		case 22: return SL(TEXT("Koło barw"), TEXT("Colour wheel"));
		case 23: return SL(TEXT("Instrumenty"), TEXT("Instruments"));
		case 24: return SL(TEXT("Zdrowy talerz"), TEXT("Healthy plate"));
		case 25: return SL(TEXT("Sport"), TEXT("Sports"));
		case 26: return SL(TEXT("Ruch i sen"), TEXT("Move & sleep"));
		case 27: return SL(TEXT("Układ Słoneczny"), TEXT("Solar System"));
		case 28: return SL(TEXT("Obieg wody"), TEXT("Water cycle"));
		default: return SL(TEXT("Magnes i prąd"), TEXT("Magnets & circuits"));
		}
	}
	if (Caption >= KornikCaption && Caption < KornikCaption + KornikPosterKinds)
	{
		// ArtKornik.KornikPosterTitle
		switch (Caption - KornikCaption)
		{
		case 0: return SL(TEXT("Łabędzie na jeziorze"), TEXT("Swans on the lake"));
		case 1: return SL(TEXT("Arboretum Kórnickie"), TEXT("Kórnik Arboretum"));
		case 2: return SL(TEXT("Ratusz w Bninie 1777"), TEXT("Bnin Town Hall 1777"));
		case 3: return SL(TEXT("Biała Dama"), TEXT("The White Lady"));
		case 4: return SL(TEXT("Biblioteka Kórnicka"), TEXT("Kórnik Library"));
		case 5: return SL(TEXT("Mapa Kórnika"), TEXT("Map of Kórnik"));
		case 6: return SL(TEXT("Ogród Teofili"), TEXT("Teofila's garden"));
		default: return SL(TEXT("Uczymy się razem!"), TEXT("We learn together!"));
		}
	}
	// The original paintings had no written title (they were recognisable portraits); these name the sitter.
	switch (Caption)
	{
	case PaintWhiteLady: return SL(TEXT("Biała Dama"), TEXT("The White Lady"));
	case PaintTytus: return TEXT("Tytus Działyński");
	case PaintCelina: return TEXT("Celina z Zamoyskich Działyńska");
	case PaintJan: return TEXT("Jan Kanty Działyński");
	case PaintCastle: return SL(TEXT("Zamek w Kórniku"), TEXT("Kórnik Castle"));
	default: return FString();
	}
}

FString FLTSchool::CaptionText(int32 Caption, bool bPolish)
{
	auto SL = [bPolish](const TCHAR* Pl, const TCHAR* En) { return FString(bPolish ? Pl : En); };
	if (Caption >= SubjectCaption && Caption < SubjectCaption + LT::Classes * 3)
	{
		// ArtSubject.SubjectPosterCaption
		switch (Caption - SubjectCaption)
		{
		case 0: return SL(TEXT("Tabliczka mnożenia: tam, gdzie spotyka się wiersz z kolumną, jest iloczyn, np. 7 × 8 = 56. Żółte pola to kwadraty liczb: 1, 4, 9, 16…"),
		                  TEXT("Times table: where a row meets a column you find the product, e.g. 7 × 8 = 56. The yellow squares are square numbers: 1, 4, 9, 16…"));
		case 1: return SL(TEXT("Trójkąt ma 3 boki, a kwadrat i prostokąt mają po 4 boki i 4 kąty proste (90°). Koło nie ma żadnych kątów!"),
		                  TEXT("A triangle has 3 sides, while a square and a rectangle have 4 sides and 4 right angles (90°). A circle has no corners at all!"));
		case 2: return SL(TEXT("Pizza podzielona na 2 równe części to połówki, na 4 – ćwiartki, a na 8 – ósemki. 1/2 to tyle samo co 2/4 i 4/8!"),
		                  TEXT("A pizza cut into 2 equal parts gives halves, into 4 – quarters, into 8 – eighths. 1/2 is the same as 2/4 and 4/8!"));
		case 3: return SL(TEXT("Polski alfabet ma 32 litery, w tym 9 liter z ogonkami i kreskami: Ą, Ć, Ę, Ł, Ń, Ó, Ś, Ź, Ż."),
		                  TEXT("The English alphabet has 26 letters: 5 vowels (A, E, I, O, U) and 21 consonants."));
		case 4: return SL(TEXT("Rzeczownik odpowiada na pytania kto? co?, czasownik – co robi?, a przymiotnik – jaki? jaka? jakie?"),
		                  TEXT("A noun names a thing (cat), a verb tells what someone does (run) and an adjective describes something (happy)."));
		case 5: return SL(TEXT("Gdy ó wymienia się na o (stół – stoły), piszemy ó. Zapamiętaj też: góra, rzeka, żaba, chleb, herbata!"),
		                  TEXT("Remember: i before e, except after c – friend and believe, but receive and ceiling. Many words ending in y change to -ies: baby → babies."));
		case 6: return SL(TEXT("Łańcuch pokarmowy: trawę zjada zając, a zająca – lis. Strzałki pokazują, dokąd płynie energia."),
		                  TEXT("A food chain: grass is eaten by the rabbit, and the rabbit by the fox. The arrows show where the energy goes."));
		case 7: return SL(TEXT("Korzeń pobiera wodę z ziemi, łodyga ją przewodzi, liście wytwarzają pokarm dzięki światłu, a z kwiatu powstają nasiona."),
		                  TEXT("The root takes water from the soil, the stem carries it, the leaves make food from sunlight and the flower makes seeds."));
		case 8: return SL(TEXT("Z jaja wykluwa się gąsienica, która zamienia się w poczwarkę, a z poczwarki wychodzi motyl. To przeobrażenie zupełne!"),
		                  TEXT("An egg hatches into a caterpillar, which turns into a pupa, and a butterfly comes out of the pupa. This change is called metamorphosis!"));
		case 9: return SL(TEXT("Polska leży nad Morzem Bałtyckim, a jej najdłuższa rzeka to Wisła. Stolicą jest Warszawa, a Kórnik leży niedaleko Poznania."),
		                  TEXT("Poland lies on the Baltic Sea and its longest river is the Vistula. The capital is Warsaw, and Kórnik is near Poznań."));
		case 10: return SL(TEXT("W 966 r. Mieszko I przyjął chrzest, w 1025 r. Bolesław Chrobry został pierwszym królem, a w 1410 r. Polacy wygrali bitwę pod Grunwaldem. Konstytucję 3 Maja uchwalono w 1791 r., a niepodległość odzyskaliśmy w 1918 r."),
		                   TEXT("In 966 Mieszko I was baptised, in 1025 Bolesław the Brave became the first king and in 1410 the Poles won the Battle of Grunwald. The Constitution of 3 May was passed in 1791 and Poland regained independence in 1918."));
		case 11: return SL(TEXT("Na Ziemi jest 7 kontynentów: Azja, Afryka, Ameryka Północna, Ameryka Południowa, Antarktyda, Europa i Australia. Największy ocean to Ocean Spokojny."),
		                   TEXT("There are 7 continents: Asia, Africa, North America, South America, Antarctica, Europe and Australia. The largest ocean is the Pacific."));
		case 12: return SL(TEXT("Hello! znaczy Cześć!, Good morning! – Dzień dobry!, a Goodbye! – Do widzenia!"),
		                   TEXT("Cześć! means Hello!, Dzień dobry! – Good morning! and Do widzenia! – Goodbye!"));
		case 13: return SL(TEXT("Red to czerwony, blue – niebieski, green – zielony, a yellow – żółty. Policz po angielsku: one, two, three, four!"),
		                   TEXT("Czerwony is red, niebieski – blue, zielony – green and żółty – yellow. Count in Polish: jeden, dwa, trzy, cztery!"));
		case 14: return SL(TEXT("Big Ben to wielki dzwon w słynnej wieży zegarowej w Londynie. Po ulicach jeżdżą czerwone piętrowe autobusy, a nad Tamizą stoi Tower Bridge."),
		                   TEXT("Big Ben is the great bell in London's famous clock tower. Red double-decker buses drive through the streets and Tower Bridge crosses the River Thames."));
		case 15: return SL(TEXT("W szachach są: król, hetman, wieże, gońce, skoczki i piony. Partia kończy się, gdy król dostanie mata!"),
		                   TEXT("Chess has a king, a queen, rooks, bishops, knights and pawns. The game ends when a king is checkmated!"));
		case 16: return SL(TEXT("Szukaj reguły: 2, 4, 8, 16 – każda liczba jest dwa razy większa od poprzedniej. Co będzie dalej w pozostałych rzędach?"),
		                   TEXT("Find the rule: 2, 4, 8, 16 – each number is double the one before. What comes next in the other rows?"));
		case 17: return SL(TEXT("Tangram to chińska układanka z 7 części: 5 trójkątów, kwadratu i równoległoboku. Ułożysz z nich kota, domek albo łódkę!"),
		                   TEXT("A tangram is a Chinese puzzle of 7 pieces: 5 triangles, a square and a parallelogram. You can make a cat, a house or a boat!"));
		case 18: return SL(TEXT("Krótka wskazówka pokazuje godziny, a długa – minuty. 7:30 to wpół do ósmej, a 12:15 to kwadrans po dwunastej."),
		                   TEXT("The short hand shows the hours and the long hand the minutes. 7:30 is half past seven and 12:15 is a quarter past twelve."));
		case 19: return SL(TEXT("Rok ma 12 miesięcy i 4 pory roku: wiosnę, lato, jesień i zimę. Kalendarzowa wiosna zaczyna się 21 marca."),
		                   TEXT("A year has 12 months and 4 seasons: spring, summer, autumn and winter. In Poland spring starts around 21 March."));
		case 20: return SL(TEXT("Rano słońce wschodzi, w południe stoi najwyżej na niebie, wieczorem zachodzi, a w nocy świeci księżyc."),
		                   TEXT("In the morning the Sun rises, at noon it is highest in the sky, in the evening it sets and at night the Moon shines."));
		case 21: return SL(TEXT("Gama C-dur to: do, re, mi, fa, sol, la, si, do (po polsku C, D, E, F, G, A, H, C). Nuty zapisujemy na pięciolinii z kluczem wiolinowym."),
		                   TEXT("The C major scale is do, re, mi, fa, sol, la, ti, do – or C, D, E, F, G, A, B, C. Notes are written on a five-line staff with a treble clef."));
		case 22: return SL(TEXT("Barwy podstawowe to czerwony, żółty i niebieski. Mieszając je, dostajemy barwy pochodne: pomarańczowy, zielony i fioletowy."),
		                   TEXT("The primary colours are red, yellow and blue. Mixing them gives the secondary colours: orange, green and purple."));
		case 23: return SL(TEXT("Skrzypce to instrument strunowy, trąbka – dęty, a bęben – perkusyjny. Każda rodzina wydaje dźwięk w inny sposób!"),
		                   TEXT("The violin is a string instrument, the trumpet a wind instrument and the drum a percussion instrument. Each family makes sound in a different way!"));
		case 24: return SL(TEXT("Połowę talerza niech zajmą warzywa i owoce, a resztę – produkty zbożowe i białko. Do picia najlepsza jest woda!"),
		                   TEXT("Fill half of your plate with vegetables and fruit, and the rest with grains and protein. Water is the best drink!"));
		case 25: return SL(TEXT("Piłka nożna, pływanie, jazda na rowerze i koszykówka to świetne sposoby na ruch. Który sport lubisz najbardziej?"),
		                   TEXT("Football, swimming, cycling and basketball are great ways to keep moving. Which sport do you like best?"));
		case 26: return SL(TEXT("Dzieci potrzebują co najmniej 60 minut ruchu dziennie i około 9–10 godzin snu każdej nocy. Wtedy rosną zdrowo i lepiej się uczą!"),
		                   TEXT("Children need at least 60 minutes of activity a day and about 9–10 hours of sleep every night. Then they grow healthy and learn better!"));
		case 27: return SL(TEXT("Wokół Słońca krąży 8 planet: Merkury, Wenus, Ziemia, Mars, Jowisz, Saturn, Uran i Neptun. Jowisz jest największy, a Saturn ma piękne pierścienie."),
		                   TEXT("8 planets orbit the Sun: Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus and Neptune. Jupiter is the biggest and Saturn has beautiful rings."));
		case 28: return SL(TEXT("Słońce ogrzewa wodę, która paruje i tworzy chmury. Z chmur pada deszcz, a rzeki niosą wodę z powrotem do morza."),
		                   TEXT("The Sun warms the water, which evaporates and forms clouds. Rain falls from the clouds and rivers carry the water back to the sea."));
		default: return SL(TEXT("Różne bieguny magnesu (N i S) się przyciągają, a jednakowe odpychają. Gdy włącznik jest zamknięty, prąd z baterii płynie i żarówka świeci."),
		                   TEXT("Opposite poles of a magnet (N and S) attract and like poles repel. When the switch is closed, current flows from the battery and the bulb lights up."));
		}
	}
	if (Caption >= KornikCaption && Caption < KornikCaption + KornikPosterKinds)
	{
		// ArtKornik.KornikPosterCaption
		switch (Caption - KornikCaption)
		{
		case 0: return SL(TEXT("Po Jeziorze Kórnickim pływają białe łabędzie. Nad jeziorem stoi Zamek w Kórniku."),
		                  TEXT("White swans swim on Lake Kórnik. Kórnik Castle stands by the lake."));
		case 1: return SL(TEXT("Arboretum Kórnickie to wielki ogród drzew. Rosną w nim drzewa z całego świata!"),
		                  TEXT("The Kórnik Arboretum is a big garden of trees. Trees from all over the world grow there!"));
		case 2: return SL(TEXT("Teofila, patronka naszej szkoły, kazała zbudować ratusz w Bninie w 1777 roku."),
		                  TEXT("Teofila, the patron of our school, had the Bnin town hall built in 1777."));
		case 3: return SL(TEXT("Teofila z Działyńskich Szołdrska-Potulicka (1714–1790) to patronka naszej szkoły. Legenda mówi, że o północy jako Biała Dama wychodzi ze swojego portretu i spaceruje po zamku."),
		                  TEXT("Teofila z Działyńskich Szołdrska-Potulicka (1714–1790) is the patron of our school. The legend says that at midnight she steps out of her portrait as the White Lady and walks around the castle."));
		case 4: return SL(TEXT("Bibliotekę Kórnicką założył Tytus Działyński. Można w niej zobaczyć bardzo stare książki."),
		                  TEXT("Tytus Działyński founded the Kórnik Library. Very old books are kept there."));
		case 5: return SL(TEXT("Na mapie widać długie Jezioro Kórnickie, zamek, Arboretum, Bnin i naszą szkołę SP 2."),
		                  TEXT("The map shows the long Lake Kórnik, the castle, the Arboretum, Bnin and our school SP 2."));
		case 6: return SL(TEXT("W ogrodzie Teofili woda tryskała z pysków krokodyli, wielorybów i lwów. Po kanale pływały łabędzie, a po dziedzińcu chodziły żurawie, pawie i bażanty."),
		                  TEXT("In Teofila's garden water flowed from the mouths of crocodiles, whales and lions. Swans swam on the canal, and cranes, peacocks and pheasants walked in the courtyard."));
		default: return SL(TEXT("Szkoła Podstawowa nr 2 im. Teofili z Działyńskich Szołdrskiej-Potulickiej w Kórniku. Uczymy się razem!"),
		                   TEXT("Primary School No. 2 named after Teofila z Działyńskich Szołdrska-Potulicka in Kórnik. We learn together!"));
		}
	}
	// Lang.cs "paint0".."paint4"
	switch (Caption)
	{
	case PaintWhiteLady: return SL(TEXT("Teofila z Działyńskich Szołdrska-Potulicka (1714-1790) - patronka naszej szkoły. To ona jest Białą Damą z zamku w Kórniku!"),
	                               TEXT("Teofila z Działyńskich Szołdrska-Potulicka (1714-1790) - our school's patron. She is the White Lady of Kórnik Castle!"));
	case PaintTytus: return SL(TEXT("Tytus Działyński (1796-1861) - założyciel Biblioteki Kórnickiej, przebudował zamek w Kórniku."),
	                           TEXT("Tytus Działyński (1796-1861) - founder of the Kórnik Library, he rebuilt Kórnik Castle."));
	case PaintCelina: return SL(TEXT("Celina z Zamoyskich Działyńska - żona Tytusa Działyńskiego."),
	                            TEXT("Celina z Zamoyskich Działyńska - wife of Tytus Działyński."));
	case PaintJan: return SL(TEXT("Jan Kanty Działyński (1829-1880) - syn Tytusa, kolekcjoner i właściciel zamku w Kórniku."),
	                         TEXT("Jan Kanty Działyński (1829-1880) - Tytus's son, a collector and owner of Kórnik Castle."));
	case PaintCastle: return SL(TEXT("Zamek w Kórniku nad Jeziorem Kórnickim. Legenda mówi, że o północy Biała Dama wychodzi z obrazu i spaceruje po zamku."),
	                            TEXT("Kórnik Castle by Lake Kórnik. Legend says that at midnight the White Lady steps out of her painting and walks around the castle."));
	default: return FString();
	}
}

FLTFloorLayout FLTSchool::BuildLayout(int32 Floor, bool bPolish)
{
	Floor = FMath::Clamp(Floor, 0, LT::Floors - 1);
	FLTFloorLayout L;
	L.Floor = Floor;
	L.Cells.Init(ELTCell::Wall, LT::MapW * LT::MapH);
	L.Surfaces.Init(ELTSurface::None, LT::MapW * LT::MapH);
	L.Rooms.SetNum(LT::Classes);
	FBuilder B(L);
	using F = ELTFace;
	using D = ELTWallDecor;

	// ---- carve the floor plan (everything starts as wall)
	B.Carve(1, 1, 5, 19, ELTCell::Lobby, ELTSurface::Marble);
	B.Carve(1, 9, 40, 11, ELTCell::Corridor, ELTSurface::Checker);
	B.Carve(1, 9, 5, 11, ELTCell::Lobby, ELTSurface::Checker);     // the corridor's floor runs into the lobby
	for (int32 K = 0; K < 5; K++)
	{
		const int32 X0 = 7 + 7 * K;
		B.Carve(X0, 1, X0 + 5, 7, ELTCell::Classroom, ELTSurface::Parquet);       // top classroom
		B.Carve(X0, 13, X0 + 5, 19, ELTCell::Classroom, ELTSurface::Parquet);     // bottom classroom
		B.Carve(X0 + 2, 8, X0 + 3, 8, ELTCell::Doorway, ELTSurface::Parquet);     // doors
		B.Carve(X0 + 2, 12, X0 + 3, 12, ELTCell::Doorway, ELTSurface::Parquet);
	}
	B.Carve(42, 1, 52, 19, ELTCell::MasterHall, ELTSurface::Carpet);          // Master Turtle's hall
	for (int32 X = 6; X <= 40; X++) L.Surfaces[10 * LT::MapW + X] = ELTSurface::Runner;     // swan carpet along the corridor
	for (int32 I = 0; I < 9; I++) L.Surfaces[(9 + I / 3) * LT::MapW + 2 + I % 3] = ELTSurface::Mosaic;   // "SP 2 KÓRNIK" floor mosaic
	L.MasterDoor = FIntPoint(41, 10);
	L.Stairs = FIntPoint(53, 10);
	L.Cells[10 * LT::MapW + 41] = ELTCell::MasterDoor;
	L.Surfaces[10 * LT::MapW + 41] = ELTSurface::Carpet;      // the floor once the door opens
	L.Cells[10 * LT::MapW + 53] = ELTCell::Stairs;
	L.Surfaces[10 * LT::MapW + 53] = ELTSurface::Carpet;

	// ---- dress the walls (per face, so a wall looks different from the corridor and from the room)
	FRandomStream Rnd(Floor * 31 + 7);
	for (int32 K = 0; K < 5; K++)
	{
		const int32 X0 = 7 + 7 * K;
		for (int32 Side = 0; Side < 2; Side++)
		{
			const int32 Room = Side == 0 ? K : 5 + K;
			const int32 Subject = SubjectOf(Floor, Room);
			const FLTSubject& Subj = Subjects()[Subject];
			const int32 WallRow = Side == 0 ? 8 : 12, FarRow = Side == 0 ? 0 : 20;
			const F CorridorFace = Side == 0 ? F::S : F::N;
			const F FrontFace = Side == 0 ? F::S : F::N;     // the far wall seen from inside the room

			// corridor side: poster | sign | door | door | lockers | lockers
			const int32 KP = (Room + Floor * 3) % KornikPosterKinds;
			B.SetPainting(X0, WallRow, CorridorFace, D::KornikPoster, KP, KornikCaption + KP);
			if (FLTWallDecor* Sign = B.SetFace(X0 + 1, WallRow, CorridorFace, D::RoomSign, 0, Room))
				Sign->Text = RoomLabel(Floor, Room) + TEXT("\n") + Subj.Name(bPolish);
			B.SetFace(X0 + 4, WallRow, CorridorFace, D::Lockers, Rnd.RandRange(0, 2));
			B.SetFace(X0 + 5, WallRow, CorridorFace, D::Lockers, Rnd.RandRange(0, 2));
			// classroom: blackboard on the front wall, windows, bookshelves and posters
			for (int32 Half = 0; Half < 2; Half++)
				if (FLTWallDecor* Board = B.SetFace(X0 + 2 + Half, FarRow, FrontFace, D::Blackboard, Half, Room))
					Board->Text = Subj.Name(bPolish);
			B.SetFace(X0, FarRow, FrontFace, D::Window);
			B.SetFace(X0 + 1, FarRow, FrontFace, D::Poster, (Room + Floor) % 6);
			B.SetFace(X0 + 4, FarRow, FrontFace, D::Bookshelf);
			B.SetFace(X0 + 5, FarRow, FrontFace, D::Window);
			// side walls: the subject's three posters (walk up to one to read its caption), shelves and windows;
			// rows 4-5 are kept free of artifacts so the posters stay visible
			const int32 Caption = SubjectCaption + Subject * 3;
			for (int32 Y = 1; Y <= 7; Y++)
			{
				const int32 RY = Side == 0 ? Y : 20 - Y;
				if (Y == 4 || Y == 5) B.SetPainting(X0 - 1, RY, F::E, D::SubjectPoster, Y - 4, Caption + Y - 4, Room);
				else if (Y == 2) B.SetFace(X0 - 1, RY, F::E, D::Bookshelf);
				else if (Y == 6) B.SetFace(X0 - 1, RY, F::E, D::Notice);
				else B.ClearFace(X0 - 1, RY, F::E);
				if (Y == 4) B.SetPainting(X0 + 6, RY, F::W, D::SubjectPoster, 2, Caption + 2, Room);
				else if (Y == 2 || Y == 6) B.SetFace(X0 + 6, RY, F::W, D::Window);
				else B.ClearFace(X0 + 6, RY, F::W);
			}
		}
	}
	// lobby: the school banner over three cells
	const FString BannerText = bPolish
		? FString(TEXT("Szkoła Podstawowa nr 2 w Kórniku\nim. Teofili z Działyńskich Szołdrskiej-Potulickiej"))
		: FString(TEXT("Primary School No. 2 in Kórnik\nnamed after Teofila z Działyńskich Szołdrska-Potulicka"));
	for (int32 I = 0; I < 3; I++)
		if (FLTWallDecor* Banner = B.SetFace(2 + I, 0, F::S, D::Banner, I))
			Banner->Text = BannerText;
	// lobby south wall: Kórnik pictures and the map
	B.SetPainting(2, 20, F::N, D::KornikPoster, 1, KornikCaption + 1);
	B.SetPainting(3, 20, F::N, D::KornikPoster, 5, KornikCaption + 5);
	B.SetPainting(4, 20, F::N, D::KornikPoster, 7, KornikCaption + 7);
	// the always-open door to the schoolyard (on every floor)
	B.SetFace(0, 10, F::E, D::YardDoor);
	L.YardDoor = FIntPoint(0, 10);
	B.SetFace(0, 13, F::E, D::DisplayCase);
	B.SetFace(0, 4, F::E, D::Window);
	B.SetFace(0, 16, F::E, D::Window);
	B.SetPainting(6, 3, F::W, D::Painting, PaintCastle, PaintCastle);
	B.SetPainting(6, 17, F::W, D::Painting, PaintWhiteLady, PaintWhiteLady);
	B.SetFace(6, 6, F::W, D::Notice);
	// master hall: royal wallpaper on every wall face that can be seen (the west faces of x = 41 belong to the last classroom)
	for (int32 Y = 0; Y < LT::MapH; Y++)
		for (int32 X = 41; X < LT::MapW; X++)
		{
			if (L.At(X, Y) != ELTCell::Wall) continue;      // also skips the master door and the stairs (World.SetWall clears them)
			for (int32 Fi = 0; Fi < 4; Fi++)
			{
				const F Face = (F)Fi;
				if (X == 41 && Face != F::E) continue;
				const FIntPoint N = FaceNeighbour(X, Y, Face);
				if (L.InBounds(N.X, N.Y) && L.At(N.X, N.Y) != ELTCell::Wall)
					B.SetFace(X, Y, Face, D::RoyalWall);
			}
		}
	B.SetFace(41, 9, F::W, D::DisplayCase);
	B.SetFace(41, 11, F::W, D::DisplayCase);
	// the Działyński gallery in the Master Turtle's hall (paintings on the royal wallpaper)
	B.SetPainting(44, 0, F::S, D::Painting, PaintCastle, PaintCastle);
	B.SetPainting(47, 0, F::S, D::Painting, PaintWhiteLady, PaintWhiteLady);
	B.SetPainting(50, 0, F::S, D::Painting, PaintTytus, PaintTytus);
	B.SetPainting(44, 20, F::N, D::Painting, PaintCelina, PaintCelina);
	B.SetPainting(47, 20, F::N, D::Painting, PaintJan, PaintJan);
	B.SetPainting(50, 20, F::N, D::Painting, PaintWhiteLady, PaintWhiteLady);
	B.SetPainting(41, 4, F::E, D::Painting, PaintTytus, PaintTytus);
	B.SetPainting(41, 16, F::E, D::Painting, PaintCelina, PaintCelina);
	B.ClearCell(41, 10);
	B.ClearCell(53, 10);

	// ---- people and furniture
	for (int32 K = 0; K < 5; K++)
		for (int32 Side = 0; Side < 2; Side++)
			BuildClassroom(B, Floor, K, Side, bPolish, Rnd);

	L.MasterPos = FVector2D(48.5, 10.5);
	FLTPropSpawn& Master = B.Add(ELTProp::Master, TEXT("master"), 48.5, 10.5, 0, 1.0, 1.25);
	Master.Yaw = UE_DOUBLE_PI;
	FBuilder::SetBox(Master, 0.35, 0.35);
	FLTPropSpawn& Throne = B.Add(ELTProp::Throne, TEXT("throne"), 50.2, 10.5, 0, 1.2, 1.5);
	Throne.Yaw = UE_DOUBLE_PI;            // faces the door like the Master Turtle (the original was a flat sprite)
	FBuilder::SetBox(Throne, 0.5, 0.3);
	B.Add(ELTProp::MasterMarker, TEXT("marker"), 48.5, 10.5, 1.32, 0.3, 0.3);
	for (const FVector2D& P : { FVector2D(43.0, 2.0), FVector2D(52.0, 2.0), FVector2D(43.0, 19.0), FVector2D(52.0, 19.0) })
		B.Plant(P.X, P.Y, 0.6, 0.85);
	B.Add(ELTProp::Globe, TEXT("globe"), 51.5, 5.5, 0, 0.6, 0.85).Radius = 0.25;
	B.Add(ELTProp::Clock, TEXT("clock"), 51.5, 15.5, 0, 0.5, 0.95).Radius = 0.25;

	// lobby
	B.Plant(1.6, 1.6, 0.6, 0.85);
	B.Plant(5.4, 1.6, 0.6, 0.85);
	B.Plant(1.6, 19.4, 0.6, 0.85);
	B.Add(ELTProp::Fountain, TEXT("fountain"), 1.5, 6.5, 0, 0.55, 0.8).Radius = 0.25;
	B.Bench(3.5, 1.4, 1.0, UE_DOUBLE_PI / 2);
	B.Bench(3.5, 19.6, 1.0, -UE_DOUBLE_PI / 2);
	// corridor benches and plants by the dividing walls
	for (int32 K = 0; K < 4; K++)
	{
		B.Plant(13.5 + 7 * K, 9.25, 0.5, 0.7);
		B.Bench(13.5 + 7 * K, 11.75, 0.9, -UE_DOUBLE_PI / 2);
	}
	// coins (some floating: jump for them!)
	for (int32 X = 8; X <= 38; X += 3)
		B.Coin(X + 0.5, 10.5, X % 2 == 0 ? 0.5 : 0.08);
	for (int32 Y = 3; Y <= 17; Y += 2)
		B.Coin(3.5, Y + 0.5, Y % 4 == 1 ? 0.5 : 0.08);
	// classmates walking around the corridor
	for (int32 I = 0; I < 4; I++)
	{
		const int32 V = I % 3;
		FLTPropSpawn& Kid = B.Add(ELTProp::Kid, *FString::Printf(TEXT("kid%d"), V), 6 + I * 9.3, I % 2 == 0 ? 9.6 : 11.4, 0, 0.5, 0.58);
		Kid.Walk = I % 2 == 0 ? 1 : -1;
		Kid.Variant = V;
		Kid.Yaw = Kid.Walk > 0 ? 0 : UE_DOUBLE_PI;
		Kid.Name = KidName(1 + Floor * 4 + I, bPolish);
	}
	L.KidMinX = 2;
	L.KidMaxX = 39;

	L.Start = FVector2D(3, 10.5);
	L.StartYaw = 0;
	return L;
}
