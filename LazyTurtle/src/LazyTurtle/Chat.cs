using System;

namespace LazyTurtle
{
    /// <summary>
    /// What classmates say when the turtle talks to them: names, friendly tips and Kórnik facts,
    /// riddles (zagadki) and the football match offer. Polish and English (L.Pl).
    /// </summary>
    static class Chat
    {
        static readonly string[] NamesPl = { "Kuba", "Ola", "Staś", "Zosia", "Antek", "Hania", "Franek", "Lena", "Tymek", "Maja", "Jaś", "Pola" };
        static readonly string[] NamesEn = { "Jack", "Olivia", "Sam", "Sophie", "Tony", "Hannah", "Frank", "Lena", "Tim", "Maya", "Johnny", "Polly" };

        /// <summary>First name of classmate number index (wraps around).</summary>
        public static string KidName(int index)
        {
            var n = L.Pl ? NamesPl : NamesEn;
            return n[((index % n.Length) + n.Length) % n.Length];
        }

        /// <summary>The football captain in the schoolyard.</summary>
        public static string CaptainName => KidName(0);

        // ---- small talk: [pl, en]
        static readonly string[][] SchoolLines =
        {
            new[] { "Drzwi z napisem BOISKO w holu prowadzą na podwórko. Gramy tam w piłkę!", "The PLAYGROUND door in the lobby leads outside. We play football there!" },
            new[] { "Mistrz Żółw czeka na końcu korytarza, ale dopiero gdy zbierzesz 10 prezentów.", "Master Turtle waits at the end of the corridor - but only after you collect 10 gifts." },
            new[] { "Wciśnij R, a pytanie zostanie przeczytane na głos.", "Press R and the question will be read aloud." },
            new[] { "Niektóre monety wiszą w powietrzu. Podskocz, żeby je złapać!", "Some coins float in the air. Jump to grab them!" },
            new[] { "Wiesz, że zamek w Kórniku stoi tuż nad jeziorem? Wygląda jak z bajki!", "Did you know Kórnik Castle stands right by the lake? It looks like a fairy tale!" },
            new[] { "Podobno nocą po zamku spaceruje Biała Dama. Ale ona jest bardzo miła i lubi dzieci!", "They say the White Lady walks around the castle at night. But she is very kind and loves children!" },
            new[] { "Biała Dama to Teofila, patronka naszej szkoły. Mówią, że pilnuje, żeby wszyscy byli grzeczni.", "The White Lady is Teofila, the patron of our school. They say she makes sure everyone is good." },
            new[] { "W Arboretum rosną drzewa z całego świata. Byłem tam z tatą na rowerze!", "The Arboretum has trees from all over the world. I rode there on my bike with my dad!" },
            new[] { "Teofila kazała zbudować ratusz w Bninie w 1777 roku. To bardzo dawno temu!", "Teofila had the Bnin town hall built in 1777. That was a very long time ago!" },
            new[] { "W ogrodzie Teofili były fontanny. Woda leciała z pyszczków lwów, krokodyli i wielorybów!", "Teofila's garden had fountains. Water came out of the mouths of lions, crocodiles and whales!" },
            new[] { "Na dziedzińcu u Teofili spacerowały pawie. Ciekawe, czy też tak głośno krzyczały?", "Peacocks walked in Teofila's courtyard. I wonder if they were that loud back then?" },
            new[] { "Wiesz, dlaczego żółw nosi dom na plecach? Bo nie ma kluczy!", "Do you know why a turtle carries its house? Because it has no keys!" },
            new[] { "Co mówi zero do ósemki? Fajny pasek!", "What did zero say to eight? Nice belt!" },
            new[] { "Dlaczego książka do matematyki jest smutna? Bo ma za dużo problemów!", "Why was the maths book sad? It had too many problems!" },
            new[] { "Na przerwie zawsze jem kanapkę z serem. A ty?", "At break time I always eat a cheese sandwich. And you?" },
            new[] { "Nasza pani mówi, że kto pyta, ten nie błądzi. Więc pytaj śmiało!", "Our teacher says asking questions is the best way to learn. So ask away!" },
            new[] { "Po lekcjach idę na lody nad jezioro. Kórnickie lody są najlepsze!", "After school I'm getting ice cream by the lake. Kórnik ice cream is the best!" },
            new[] { "Za dobre odpowiedzi dostajesz gwiazdki. Zbierz wszystkie!", "You get stars for good answers. Collect them all!" },
        };

        static readonly string[][] YardLines =
        {
            new[] { "Porozmawiaj z kapitanem, żeby zagrać mecz!", "Talk to the captain to play a match!" },
            new[] { "Kapitan {c} szuka kogoś do drużyny. Może ty?", "Captain {c} is looking for a team mate. Maybe you?" },
            new[] { "Biegnij na piłkę, a będziesz ją prowadzić. Stań tuż przy niej, żeby mocno kopnąć!", "Run into the ball to dribble it. Stand right next to it to give it a big kick!" },
            new[] { "Zobacz fontannę z lwami! Woda leci im z pyszczków, jak w ogrodzie Teofili.", "Look at the lion fountain! Water comes out of their mouths, like in Teofila's garden." },
            new[] { "Pawie są piękne, ale nie goń ich, proszę. Czasem rozkładają ogon!", "The peacocks are beautiful, but please don't chase them. Sometimes they open their tails!" },
            new[] { "Łabędzie na stawie są bardzo dostojne. Nie wchodź do wody!", "The swans on the pond are very graceful. Don't go into the water!" },
            new[] { "Stąd widać zamek w Kórniku nad jeziorem. Pięknie, prawda?", "You can see Kórnik Castle over the lake from here. Beautiful, isn't it?" },
            new[] { "Te drzewa są jak małe Arboretum. Ten wysoki to chyba dąb!", "These trees are like a little Arboretum. The tall one is an oak, I think!" },
            new[] { "Drzwi szkoły są tam, na południu. Wrócisz nimi do holu.", "The school door is over there, to the south. It takes you back to the lobby." },
            new[] { "Przy ścieżkach leżą monety. Niektóre trzeba złapać w podskoku!", "There are coins along the paths. Some you have to catch with a jump!" },
            new[] { "Wygrany mecz to aż 10 monet!", "Winning a match gives you 10 coins!" },
        };

        /// <summary>A random friendly line. In the yard most lines are about the yard and the match.</summary>
        public static string Line(Random r, bool inYard)
        {
            var set = inYard && r.Next(4) != 0 ? YardLines : SchoolLines;
            var s = set[r.Next(set.Length)][L.Pl ? 0 : 1];
            return s.Replace("{c}", CaptainName);
        }

        // ---- riddles: question, correct answer, two wrong ones
        static readonly string[][] RiddlesPl =
        {
            new[] { "Co ma klucze, ale nie otwiera drzwi?", "pianino", "szafa", "samochód" },
            new[] { "Co ma zęby, ale nie gryzie?", "grzebień", "pies", "rekin" },
            new[] { "Co biegnie, ale nie ma nóg?", "rzeka", "koń", "żółw" },
            new[] { "Co ma szyję, ale nie ma głowy?", "butelka", "żyrafa", "kot" },
            new[] { "Co ma oczko, ale nic nie widzi?", "igła", "sowa", "kot" },
            new[] { "Im więcej z niej zabierasz, tym większa się robi. Co to?", "dziura", "góra", "woda" },
            new[] { "Co ma ręce, ale nie może klaskać?", "zegar", "małpa", "robot" },
            new[] { "Co ma liście, ale nie jest drzewem?", "książka", "dąb", "kwiat" },
            new[] { "Co można złapać, ale nie można rzucić?", "katar", "piłkę", "kamień" },
            new[] { "Co idzie w górę i w dół, a nie rusza się z miejsca?", "schody", "winda", "piłka" },
            new[] { "Co jest mokre, kiedy suszy?", "ręcznik", "słońce", "wiatr" },
            new[] { "Ma cztery nogi, ale nie chodzi. Co to?", "stół", "pies", "krowa" },
            new[] { "Co ma kapelusz, ale nie ma głowy?", "grzyb", "pan", "bałwan" },
        };

        static readonly string[][] RiddlesEn =
        {
            new[] { "What has keys but can't open a door?", "a piano", "a wardrobe", "a car" },
            new[] { "What has teeth but can't bite?", "a comb", "a dog", "a shark" },
            new[] { "What runs but has no legs?", "a river", "a horse", "a turtle" },
            new[] { "What has a neck but no head?", "a bottle", "a giraffe", "a cat" },
            new[] { "What has an eye but cannot see?", "a needle", "an owl", "a cat" },
            new[] { "The more you take away from it, the bigger it gets. What is it?", "a hole", "a mountain", "water" },
            new[] { "What has hands but can't clap?", "a clock", "a monkey", "a robot" },
            new[] { "What has leaves but is not a tree?", "a book", "an oak", "a flower" },
            new[] { "What can you catch but not throw?", "a cold", "a ball", "a stone" },
            new[] { "What goes up and down but doesn't move?", "stairs", "a lift", "a ball" },
            new[] { "What gets wet while it dries?", "a towel", "the sun", "the wind" },
            new[] { "It has four legs but can't walk. What is it?", "a table", "a dog", "a cow" },
            new[] { "What has a cap but no head?", "a mushroom", "a man", "a snowman" },
        };

        /// <summary>A random riddle with 3 shuffled answers.</summary>
        public static Question Riddle(Random r)
        {
            var set = L.Pl ? RiddlesPl : RiddlesEn;
            var d = set[r.Next(set.Length)];
            var answers = new[] { d[1], d[2], d[3] };
            for (int i = answers.Length - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                var t = answers[i];
                answers[i] = answers[j];
                answers[j] = t;
            }
            return new Question { Text = d[0], Answers = answers, Correct = Array.IndexOf(answers, d[1]) };
        }

        // ---- dialog phrases
        public static string FootballOffer(string teammate, string a, string b) => L.Pl
            ? "Hej! Zagramy mecz do 3 goli? Ty i " + teammate + " kontra " + a + " i " + b + "!"
            : "Hey! Shall we play a match to 3 goals? You and " + teammate + " against " + a + " and " + b + "!";

        public static string Yes => L.Pl ? "Tak, gramy!" : "Yes, let's play!";
        public static string No => L.Pl ? "Może później" : "Maybe later";
        public static string RiddleIntro => L.Pl ? "Mam dla ciebie zagadkę!" : "I have a riddle for you!";
        public static string RiddleRight => L.Pl ? "Brawo! Dostajesz 3 monety!" : "Well done! You get 3 coins!";

        public static string RiddleWrong(string answer) => L.Pl
            ? "Prawie! Prawidłowa odpowiedź to: " + answer + ". Następna zagadka będzie twoja!"
            : "Almost! The right answer is: " + answer + ". The next riddle is yours!";

        public static string MatchWon => L.Pl ? "Wygraliśmy! Super gra! Dostajesz 10 monet!" : "We won! Great game! You get 10 coins!";
        public static string MatchLost => L.Pl ? "Tym razem przegraliśmy, ale było fajnie! Masz 3 monety na pocieszenie." : "We lost this time, but it was fun! Here are 3 coins anyway.";
        public static string MatchDraw => L.Pl ? "Remis! Obie drużyny grały świetnie. Dostajesz 5 monet!" : "A draw! Both teams played great. You get 5 coins!";

        public static string Greeting(string name) => L.Pl ? "Cześć! Jestem " + name + "." : "Hi! I'm " + name + ".";
    }
}
