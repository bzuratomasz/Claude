using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LazyTurtle
{
    sealed class Question
    {
        public string Text;
        public string[] Answers;
        public int Correct;
        public string CorrectText => Answers[Correct];
    }

    /// <summary>
    /// Quiz questions for children aged 8-11 (Polish grades 2-5), in Polish and English. Maths, logic, letters
    /// and words, English/Polish vocabulary and time are generated (endless variety, difficulty grows with the
    /// floor); the other subjects come from hand-written lists. Difficulty 1..9: classrooms on floor f (0..7) use
    /// f + 1 (1 = age 8, start of grade 2 ... 8 = age 11, end of grade 5); the Master Turtle uses f + 2 (up to 9).
    /// </summary>
    static partial class Questions
    {
        public const int MaxDifficulty = 9;

        /// <summary>Questions already asked in this session ("text|correct answer"), so they do not repeat.</summary>
        static readonly HashSet<string> asked = new HashSet<string>();

        public static Question Make(int subject, int difficulty, Random r)
        {
            difficulty = Clamp(difficulty);
            return Unique(() => Generate(subject, difficulty, r));
        }

        /// <summary>
        /// A harder question for the Master Turtle (one level above the floor's classrooms, any subject; quite often
        /// geography and history, and sometimes about Kórnik and our school).
        /// </summary>
        public static Question Master(int floor, Random r)
        {
            int d = Clamp(floor + 2);
            if (r.Next(4) == 0) return Unique(() => FromList(Kornik, d, r, MaxDifficulty));
            int subject = r.Next(4) == 0 ? 3 : new[] { 0, 0, 5, 5, 6, 2, 9, 1, 3, 7, 8, 4 }[r.Next(12)];
            return Make(subject, d, r);
        }

        static int Clamp(int d) => Math.Max(1, Math.Min(MaxDifficulty, d));

        static string Key(Question q) => q.Text + "|" + q.CorrectText;

        static Question Unique(Func<Question> make)
        {
            Question q = null;
            for (int attempt = 0; attempt < 25; attempt++)
            {
                q = make();
                if (asked.Add(Key(q))) break;
            }
            return q;
        }

        static Question Generate(int subject, int d, Random r)
        {
            switch (subject)
            {
                case 0: return Maths(d, r);
                case 1: return Letters(d, r);
                case 4: return Words(d, r);
                case 5: return Logic(d, r);
                case 6: return Time(d, r);
                default: return FromList(Lists[Math.Max(0, Math.Min(Lists.Length - 1, subject))] ?? Nature, d, r);
            }
        }

        static bool Pl => L.Pl;

        static string T(string pl, string en) => Pl ? pl : en;

        /// <summary>Builds a question: the correct answer plus up to three different wrong ones, shuffled.</summary>
        static Question Build(string text, string correct, IEnumerable<string> wrong, Random r)
        {
            var answers = new List<string> { correct };
            foreach (var w in wrong)
                if (!string.IsNullOrEmpty(w) && !answers.Contains(w) && answers.Count < 4) answers.Add(w);
            for (int i = answers.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                var t = answers[i];
                answers[i] = answers[j];
                answers[j] = t;
            }
            return new Question { Text = text, Answers = answers.ToArray(), Correct = answers.IndexOf(correct) };
        }

        static IEnumerable<int> NearInts(int value, Random r)
        {
            var c = new List<int> { value + 1, value - 1, value + 2, value - 2, value + 10, value - 10, value + 3 };
            return c.Where(v => v >= 0 && v != value).OrderBy(v => r.Next());
        }

        static IEnumerable<string> Near(int value, Random r) => NearInts(value, r).Select(v => v.ToString());

        /// <summary>A number question: the typical mistakes given in <paramref name="likely"/> come first, then nearby numbers.</summary>
        static Question Num(string text, int res, Random r, string unit = "", params int[] likely)
        {
            string u = unit.Length == 0 || unit == "°" ? unit : " " + unit;
            var wrong = likely.Where(v => v >= 0 && v != res).Distinct().Take(2).Concat(NearInts(res, r)).Select(v => v + u);
            return Build(text, res + u, wrong, r);
        }

        static T Pick<T>(IList<T> list, Random r) => list[r.Next(list.Count)];

        static string Lat(int n)
        {
            if (n == 1) return "rok";
            int t = n % 10, h = n % 100;
            return t >= 2 && t <= 4 && (h < 12 || h > 14) ? "lata" : "lat";
        }

        static string Roman(int n)
        {
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] s = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < v.Length; i++)
                while (n >= v[i]) { sb.Append(s[i]); n -= v[i]; }
            return sb.ToString();
        }

        static string Ordinal(int n)
        {
            int h = n % 100, t = n % 10;
            string suf = h >= 11 && h <= 13 ? "th" : t == 1 ? "st" : t == 2 ? "nd" : t == 3 ? "rd" : "th";
            return n + suf;
        }

        static string Century(int c) => Pl ? Roman(c) + " wiek" : Ordinal(c) + " century";

        /// <summary>A decimal written with a comma in Polish and a dot in English (value given in hundredths).</summary>
        static string Dec(int hundredths)
        {
            string s = (hundredths / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            return Pl ? s.Replace('.', ',') : s;
        }

        static string Money(int gr) =>
            gr >= 100 ? (gr % 100 == 0 ? gr / 100 + " zł" : gr / 100 + " zł " + gr % 100 + " gr") : gr + " gr";

        static string Hm(int minutes) => (minutes / 60) + ":" + (minutes % 60).ToString("00");

        // ------------------------------------------------------------------ maths

        static readonly string[] NamesPl = { "Ala", "Tomek", "Zosia", "Kuba", "Ola", "Staś", "Hania", "Franek" };
        static readonly string[] NamesEn = { "Amy", "Tom", "Sophie", "Jack", "Olivia", "Sam", "Emma", "Leo" };

        static Question Maths(int d, Random r)
        {
            string name = Pick(Pl ? NamesPl : NamesEn, r);
            switch (d)
            {
                case 1: return Maths1(name, r);
                case 2: return Maths2(name, r);
                case 3: return Maths3(name, r);
                case 4: return Maths4(r);
                case 5: return Maths5(r);
                case 6: return Maths6(r);
                case 7: return Maths7(r);
                case 8: return Maths8(r);
                default: return Maths9(name, r);
            }
        }

        /// <summary>Adding and subtracting within 100, multiplication table up to 5 x 10.</summary>
        static Question Maths1(string name, Random r)
        {
            int a, b;
            switch (r.Next(4))
            {
                case 0: a = r.Next(12, 80); b = r.Next(5, 100 - a + 1); return Num(a + " + " + b + " = ?", a + b, r);
                case 1: a = r.Next(30, 100); b = r.Next(6, a - 5); return Num(a + " - " + b + " = ?", a - b, r);
                case 2: a = r.Next(2, 6); b = r.Next(2, 11); return Num(a + " x " + b + " = ?", a * b, r, "", a * b + a, a * b - a);
                default:
                    a = r.Next(25, 70); b = r.Next(8, 30);
                    return r.Next(2) == 0
                        ? Num(T($"{name} ma naklejki: {a}. Dostaje jeszcze {b}. Ile naklejek ma teraz?",
                                $"{name} has {a} stickers and gets {b} more. How many stickers are there now?"), a + b, r)
                        : Num(T($"W klasowej biblioteczce są książki: {a}. Dzieci wypożyczyły {b}. Ile książek zostało?",
                                $"The class library has {a} books. The children borrow {b}. How many books are left?"), a - b, r);
            }
        }

        /// <summary>The whole multiplication table and division.</summary>
        static Question Maths2(string name, Random r)
        {
            int a = r.Next(3, 11), b = r.Next(3, 11);
            switch (r.Next(4))
            {
                case 0: return Num(a + " x " + b + " = ?", a * b, r, "", a * b + b, a * b - a);
                case 1: return Num((a * b) + " : " + a + " = ?", b, r);
                case 2: return Num(a + " x ? = " + (a * b), b, r);
                default:
                    return r.Next(2) == 0
                        ? Num(T($"Cukierki: {a * b}. Dzielimy je równo między dzieci: {a}. Ile cukierków dostanie każde dziecko?",
                                $"{a * b} sweets are shared equally between {a} children. How many sweets does each child get?"), b, r)
                        : Num(T($"{name} ustawia krzesła w rzędach. Rzędy: {a}, w każdym rzędzie krzesła: {b}. Ile jest wszystkich krzeseł?",
                                $"{name} puts chairs in {a} rows with {b} chairs in each row. How many chairs are there?"), a * b, r);
            }
        }

        /// <summary>Two-digit times one-digit numbers and money (zł / gr).</summary>
        static Question Maths3(string name, Random r)
        {
            switch (r.Next(4))
            {
                case 0:
                {
                    int a = r.Next(12, 50), b = r.Next(2, 10);
                    return Num(a + " x " + b + " = ?", a * b, r, "", a * b + b, a * b - 10);
                }
                case 1:
                {
                    int p = r.Next(1, 8) * 100 + r.Next(1, 10) * 10, n = r.Next(2, 5), tot = p * n;
                    string text = T($"Zeszyt kosztuje {Money(p)}. {name} kupuje takie zeszyty: {n}. Ile płaci?",
                                    $"A notebook costs {Money(p)}. {name} buys {n} of them. How much does that cost? (100 gr = 1 zł)");
                    return Build(text, Money(tot), new[] { tot + 100, tot - 10, tot + 10, tot - 100 }.Where(v => v > 0).Select(Money), r);
                }
                case 2:
                {
                    int note = r.Next(2) == 0 ? 1000 : 2000;
                    int price = r.Next(2, note / 100 - 1) * 100 + r.Next(1, 10) * 10, change = note - price;
                    string text = T($"Masz {Money(note)}. Kupujesz książeczkę za {Money(price)}. Ile reszty dostaniesz?",
                                    $"You have {Money(note)}. You buy a comic for {Money(price)}. How much change do you get? (100 gr = 1 zł)");
                    return Build(text, Money(change), new[] { change + 100, change - 100, change + 10, change - 10 }.Where(v => v > 0).Select(Money), r);
                }
                default:
                {
                    int z = r.Next(2, 10), g = r.Next(1, 10) * 10;
                    return Num(T($"Ile groszy to {z} zł {g} gr?", $"How many grosze (gr) are {z} zł {g} gr? (1 zł = 100 gr)"), z * 100 + g, r, "", z * 10 + g, z * 1000 + g);
                }
            }
        }

        /// <summary>Order of operations and unit conversions.</summary>
        static Question Maths4(Random r)
        {
            int a = r.Next(2, 10), b = r.Next(2, 10), c = r.Next(2, 10);
            switch (r.Next(5))
            {
                case 0: return Num($"{a} + {b} x {c} = ?", a + b * c, r, "", (a + b) * c);
                case 1: return Num($"({a} + {b}) x {c} = ?", (a + b) * c, r, "", a + b * c);
                case 2: { int x = b * c + r.Next(5, 40); return Num($"{x} - {b} x {c} = ?", x - b * c, r, "", (x - b) * c); }
                case 3: { int x = r.Next(10, 40); return Num($"{x} + {b * c} : {c} = ?", x + b, r, "", x + b * c); }
                default:
                {
                    int n = r.Next(2, 10), m = r.Next(1, 10) * 5;
                    switch (r.Next(7))
                    {
                        case 0: return Num($"{n} m = ? cm", n * 100, r, "", n * 10, n * 1000);
                        case 1: return Num($"{n} m {m + 5} cm = ? cm", n * 100 + m + 5, r, "", n * 10 + m + 5, n + m + 5);
                        case 2: return Num($"{n} kg = ? g", n * 1000, r, "", n * 100, n * 10);
                        case 3: { int g = r.Next(1, 10) * 100; return Num($"{n} kg {g} g = ? g", n * 1000 + g, r, "", n * 100 + g, n + g); }
                        case 4: return Num($"{n} h = ? min", n * 60, r, "", n * 100, n * 30);
                        case 5: return Num($"1 h {m} min = ? min", 60 + m, r, "", 100 + m, m + 1);
                        default: return Num($"{n} cm = ? mm", n * 10, r, "", n * 100, n);
                    }
                }
            }
        }

        /// <summary>Fractions and perimeters.</summary>
        static Question Maths5(Random r)
        {
            switch (r.Next(4))
            {
                case 0:
                {
                    string eq = T("są równe", "they are equal");
                    if (r.Next(2) == 0)
                    {
                        int a = r.Next(2, 11), b = r.Next(2, 11);
                        while (b == a) b = r.Next(2, 11);
                        string fa = "1/" + a, fb = "1/" + b, big = a < b ? fa : fb, small = a < b ? fb : fa;
                        return Build(T($"Która liczba jest większa: {fa} czy {fb}?", $"Which is bigger: {fa} or {fb}?"), big, new[] { small, eq }, r);
                    }
                    int n = r.Next(5, 13), x = r.Next(1, n), y = r.Next(1, n);
                    while (y == x) y = r.Next(1, n);
                    string sx = x + "/" + n, sy = y + "/" + n;
                    return Build(T($"Która liczba jest większa: {sx} czy {sy}?", $"Which is bigger: {sx} or {sy}?"), x > y ? sx : sy, new[] { x > y ? sy : sx, eq }, r);
                }
                case 1:
                {
                    int k = new[] { 2, 3, 4, 5, 10 }[r.Next(5)], part = r.Next(3, 16), whole = k * part;
                    string text = k == 2 ? T($"Ile to jest połowa z {whole}?", $"What is half of {whole}?")
                                         : T($"Ile to jest 1/{k} z {whole}?", $"What is 1/{k} of {whole}?");
                    return Num(text, part, r, "", whole / 2 == part ? part * 2 : whole / 2, whole - part);
                }
                case 2:
                {
                    int a = r.Next(4, 16), b = r.Next(2, a);
                    return Num(T($"Prostokąt ma boki {a} cm i {b} cm. Jaki ma obwód?", $"A rectangle has sides of {a} cm and {b} cm. What is its perimeter?"),
                        2 * (a + b), r, "cm", a + b, a * b);
                }
                default:
                {
                    int a = r.Next(3, 16);
                    return Num(T($"Kwadrat ma bok {a} cm. Jaki ma obwód?", $"A square has sides of {a} cm. What is its perimeter?"),
                        4 * a, r, "cm", a * a, 2 * a);
                }
            }
        }

        /// <summary>Areas, Roman numerals, three-digit adding and subtracting.</summary>
        static Question Maths6(Random r)
        {
            switch (r.Next(5))
            {
                case 0:
                {
                    int a = r.Next(3, 13), b = r.Next(2, 10);
                    return Num(T($"Prostokąt ma boki {a} cm i {b} cm. Jakie ma pole?", $"A rectangle has sides of {a} cm and {b} cm. What is its area?"),
                        a * b, r, "cm²", 2 * (a + b), a * b + a);
                }
                case 1:
                {
                    int n = r.Next(1, 101);
                    return Num(T($"Jaką liczbę zapisano cyframi rzymskimi jako {Roman(n)}?", $"Which number is written in Roman numerals as {Roman(n)}?"), n, r);
                }
                case 2:
                {
                    int n = r.Next(4, 101);
                    var wrong = new[] { n + 1, n - 1, n + 10, n - 10, n + 5 }.Where(v => v > 0 && v <= 100).OrderBy(v => r.Next()).Select(Roman);
                    return Build(T($"Jak zapisać liczbę {n} cyframi rzymskimi?", $"How do you write {n} in Roman numerals?"), Roman(n), wrong, r);
                }
                case 3: { int a = r.Next(120, 600), b = r.Next(110, 400); return Num(a + " + " + b + " = ?", a + b, r, "", a + b - 100, a + b + 100); }
                default: { int a = r.Next(400, 1000), b = r.Next(120, a - 50); return Num(a + " - " + b + " = ?", a - b, r, "", a - b + 100, a - b - 100); }
            }
        }

        static readonly int[] DecimalParts = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 25, 75, 150, 250, 125 };

        /// <summary>Decimals, simple equations and averages.</summary>
        static Question Maths7(Random r)
        {
            switch (r.Next(5))
            {
                case 0:
                {
                    int a = Pick(DecimalParts, r), b = Pick(DecimalParts, r);
                    bool plus = r.Next(2) == 0 || a == b;
                    if (!plus && a < b) { int t = a; a = b; b = t; }
                    int res = plus ? a + b : a - b;
                    var wrong = new[] { res + 10, res - 10, res + 25, res - 25, res + 100 }.Where(v => v > 0 && v != res).OrderBy(v => r.Next()).Select(Dec);
                    return Build(Dec(a) + (plus ? " + " : " - ") + Dec(b) + " = ?", Dec(res), wrong, r);
                }
                case 1:
                {
                    int a = r.Next(2, 10), big = a * 10, opts = r.Next(2);
                    var all = new[] { big, (a - 1) * 10 + 9, a, (a - 1) * 10 + (opts == 0 ? 5 : 1) };
                    return Build(T("Która liczba jest największa?", "Which number is the biggest?"), Dec(big), all.Skip(1).Select(Dec), r);
                }
                case 2:
                {
                    int x = r.Next(4, 60), a = r.Next(3, 40);
                    switch (r.Next(4))
                    {
                        case 0: return Num(T($"x + {a} = {x + a}. Ile wynosi x?", $"x + {a} = {x + a}. What is x?"), x, r, "", x + 2 * a);
                        case 1: return Num(T($"x - {a} = {x}. Ile wynosi x?", $"x - {a} = {x}. What is x?"), x + a, r, "", x - a);
                        case 2: { int m = r.Next(2, 10), y = r.Next(2, 13); return Num(T($"{m} · x = {m * y}. Ile wynosi x?", $"{m} · x = {m * y}. What is x?"), y, r, "", m * y - m); }
                        default: { int m = r.Next(2, 10), y = r.Next(2, 13); return Num(T($"x : {m} = {y}. Ile wynosi x?", $"x : {m} = {y}. What is x?"), m * y, r, "", m + y); }
                    }
                }
                case 3:
                {
                    int a = r.Next(2, 30), b = r.Next(2, 30), c = r.Next(2, 30);
                    c += (3 - (a + b + c) % 3) % 3;
                    int avg = (a + b + c) / 3;
                    return Num(T($"Jaka jest średnia liczb {a}, {b} i {c}?", $"What is the average (mean) of {a}, {b} and {c}?"), avg, r, "", a + b + c);
                }
                default:
                {
                    int a = Pick(DecimalParts, r), k = r.Next(2, 5);
                    return Build($"{k} x {Dec(a)} = ?", Dec(a * k), new[] { a * k + 10, a * k - 10, a * k * 10, a + k }.Where(v => v > 0 && v != a * k).Select(Dec), r);
                }
            }
        }

        /// <summary>Percentages, long division, angles, two-digit multiplication.</summary>
        static Question Maths8(Random r)
        {
            switch (r.Next(5))
            {
                case 0:
                {
                    int p = new[] { 50, 25, 10, 20, 75 }[r.Next(5)];
                    int n = p == 50 ? 2 * r.Next(6, 60) : p == 10 ? 10 * r.Next(2, 40) : p == 20 ? 5 * r.Next(3, 30) : 4 * r.Next(3, 30);
                    int res = n * p / 100;
                    return Num(T($"Ile to jest {p}% z {n}?", $"What is {p}% of {n}?"), res, r, "", n - res, n / 10 == res ? n / 5 : n / 10);
                }
                case 1:
                {
                    int div = r.Next(2, 10), q = r.Next(Math.Max(12, (100 + div - 1) / div), 999 / div + 1);
                    return Num((q * div) + " : " + div + " = ?", q, r, "", q + 10, q - 10);
                }
                case 2:
                {
                    switch (r.Next(3))
                    {
                        case 0:
                        {
                            int a = r.Next(30, 91), b = r.Next(20, 150 - a);
                            return Num(T($"Dwa kąty trójkąta mają {a}° i {b}°. Ile stopni ma trzeci kąt?", $"Two angles of a triangle are {a}° and {b}°. How big is the third angle?"),
                                180 - a - b, r, "°", 360 - a - b, 90 - (a + b) % 90);
                        }
                        case 1:
                        {
                            int h = new[] { 1, 2, 3, 4, 5, 6, 9 }[r.Next(7)], deg = h == 9 ? 90 : h * 30;
                            return Num(T($"Jaki kąt tworzą wskazówki zegara o godzinie {h}:00?", $"What angle do the hands of a clock make at {h}:00?"),
                                deg, r, "°", deg == 90 ? 60 : 90, deg == 180 ? 120 : 180);
                        }
                        default:
                        {
                            int k = r.Next(3);
                            string text = k == 0 ? T("Ile stopni ma kąt prosty?", "How many degrees is a right angle?")
                                        : k == 1 ? T("Ile stopni ma kąt półpełny (prosta linia)?", "How many degrees is a straight angle?")
                                                 : T("Ile stopni ma kąt pełny (cały obrót)?", "How many degrees is a full turn?");
                            int res = k == 0 ? 90 : k == 1 ? 180 : 360;
                            return Build(text, res + "°", new[] { 45, 90, 180, 360, 100 }.Where(v => v != res).Select(v => v + "°"), r);
                        }
                    }
                }
                case 3: { int a = r.Next(11, 40), b = r.Next(11, 30); return Num(a + " x " + b + " = ?", a * b, r, "", a * b + a, a * b - b); }
                default:
                {
                    int whole = r.Next(2, 9) * 12, k = r.Next(2) == 0 ? 3 : 4, part = whole / k * (k - 1);
                    return Num(T($"Ile to jest {k - 1}/{k} z {whole}?", $"What is {k - 1}/{k} of {whole}?"), part, r, "", whole / k, whole - k + 1);
                }
            }
        }

        /// <summary>Multi-step word problems.</summary>
        static Question Maths9(string name, Random r)
        {
            switch (r.Next(6))
            {
                case 0:
                {
                    int n1 = r.Next(2, 6), p1 = r.Next(2, 8), n2 = r.Next(2, 5), p2 = r.Next(2, 6), tot = n1 * p1 + n2 * p2;
                    return Num(T($"{name} kupuje zeszyty: {n1}, po {p1} zł, oraz długopisy: {n2}, po {p2} zł. Płaci banknotem 100 zł. Ile reszty dostaje?",
                                 $"{name} buys {n1} notebooks at {p1} zł each and {n2} pens at {p2} zł each, and pays with a 100 zł note. How much change is there?"),
                        100 - tot, r, "zł", tot);
                }
                case 1:
                {
                    int v = r.Next(5, 13) * 2, h = r.Next(1, 4);
                    return Num(T($"Rowerzysta jedzie ze stałą prędkością {v} km/h. Ile kilometrów przejedzie w czasie {h} godz. 30 min?",
                                 $"A cyclist rides at a steady {v} km/h. How many kilometres does she ride in {h} h 30 min?"),
                        v * h + v / 2, r, "km", v * h, v * (h + 1));
                }
                case 2:
                {
                    int price = r.Next(4, 20) * 20, p = new[] { 10, 20, 25, 50 }[r.Next(4)], cut = price * p / 100;
                    return Num(T($"Kurtka kosztowała {price} zł. Sklep obniżył cenę o {p}%. Ile kosztuje teraz?",
                                 $"A jacket cost {price} zł. The shop lowers the price by {p}%. How much does it cost now?"),
                        price - cut, r, "zł", cut, price - p);
                }
                case 3:
                {
                    int a = r.Next(6, 21), b = r.Next(3, a), c = r.Next(2, 7);
                    return Num(T($"Ogród ma kształt prostokąta o bokach {a} m i {b} m. Metr siatki kosztuje {c} zł. Ile kosztuje siatka na płot dookoła całego ogrodu?",
                                 $"A rectangular garden is {a} m by {b} m. Fencing costs {c} zł per metre. How much does a fence all the way round cost?"),
                        2 * (a + b) * c, r, "zł", (a + b) * c, a * b * c);
                }
                case 4:
                {
                    int p = r.Next(8, 21), days = r.Next(3, 8), pages = p * days + r.Next(2, 16) * 10;
                    return Num(T($"Książka ma {pages} stron. {name} czyta codziennie tyle samo stron: {p}. Ile stron zostanie do przeczytania po {days} dniach?",
                                 $"A book has {pages} pages. {name} reads {p} pages every day. How many pages are still left after {days} days?"),
                        pages - p * days, r, "", p * days);
                }
                default:
                {
                    int x = r.Next(6, 14), m = r.Next(3, 5), sum = x * (m + 1);
                    return Num(T($"Tata jest {m} razy starszy od syna. Razem mają {sum} {Lat(sum)}. Ile lat ma syn?",
                                 $"Dad is {m} times as old as his son. Together they are {sum} years old. How old is the son?"),
                        x, r, "", x * m, sum / m);
                }
            }
        }

        // ------------------------------------------------------------------ logic

        static Question Sequence(IList<int> seq, int next, Random r, params int[] likely) =>
            Num(T("Jaka liczba będzie następna?", "Which number comes next?") + "  " + string.Join(", ", seq.Select(v => v.ToString()).ToArray()) + ", ?", next, r, "", likely);

        static readonly string[][,] OddPairs =
        {
            // { word PL, word EN } x 6 for group A, then the same for group B; the odd word comes from group B
            new[,] { { "jabłko", "apple" }, { "gruszka", "pear" }, { "śliwka", "plum" }, { "banan", "banana" }, { "wiśnia", "cherry" }, { "cytryna", "lemon" },
                     { "samochód", "car" }, { "autobus", "bus" }, { "pociąg", "train" }, { "tramwaj", "tram" }, { "rower", "bike" }, { "samolot", "plane" } },
            new[,] { { "stół", "table" }, { "krzesło", "chair" }, { "szafa", "wardrobe" }, { "łóżko", "bed" }, { "kanapa", "sofa" }, { "biurko", "desk" },
                     { "gitara", "guitar" }, { "skrzypce", "violin" }, { "flet", "flute" }, { "trąbka", "trumpet" }, { "bęben", "drum" }, { "pianino", "piano" } },
            new[,] { { "czerwony", "red" }, { "zielony", "green" }, { "niebieski", "blue" }, { "żółty", "yellow" }, { "biały", "white" }, { "czarny", "black" },
                     { "pies", "dog" }, { "kot", "cat" }, { "koń", "horse" }, { "krowa", "cow" }, { "lew", "lion" }, { "owca", "sheep" } },
        };

        static readonly string[][,] OddPairsHard =
        {
            new[,] { { "wróbel", "sparrow" }, { "sowa", "owl" }, { "bocian", "stork" }, { "orzeł", "eagle" }, { "jaskółka", "swallow" }, { "dzięcioł", "woodpecker" },
                     { "lis", "fox" }, { "zając", "hare" }, { "jeż", "hedgehog" }, { "wilk", "wolf" }, { "sarna", "roe deer" }, { "bóbr", "beaver" } },
            new[,] { { "dąb", "oak" }, { "sosna", "pine" }, { "brzoza", "birch" }, { "klon", "maple" }, { "buk", "beech" }, { "świerk", "spruce" },
                     { "róża", "rose" }, { "tulipan", "tulip" }, { "stokrotka", "daisy" }, { "mak", "poppy" }, { "słonecznik", "sunflower" }, { "fiołek", "violet" } },
            new[,] { { "marchewka", "carrot" }, { "ziemniak", "potato" }, { "cebula", "onion" }, { "kapusta", "cabbage" }, { "burak", "beetroot" }, { "rzodkiewka", "radish" },
                     { "jabłko", "apple" }, { "gruszka", "pear" }, { "śliwka", "plum" }, { "banan", "banana" }, { "wiśnia", "cherry" }, { "truskawka", "strawberry" } },
            new[,] { { "Merkury", "Mercury" }, { "Wenus", "Venus" }, { "Mars", "Mars" }, { "Jowisz", "Jupiter" }, { "Saturn", "Saturn" }, { "Neptun", "Neptune" },
                     { "Afryka", "Africa" }, { "Europa", "Europe" }, { "Azja", "Asia" }, { "Australia", "Australia" }, { "Antarktyda", "Antarctica" }, { "Ameryka Południowa", "South America" } },
        };

        static Question OddWord(bool hard, Random r)
        {
            var g = Pick(hard ? OddPairsHard : OddPairs, r);
            int lang = Pl ? 0 : 1;
            bool swap = r.Next(2) == 0;
            int baseA = swap ? 6 : 0, baseB = swap ? 0 : 6;
            var same = Enumerable.Range(baseA, 6).OrderBy(i => r.Next()).Take(3).Select(i => g[i, lang]).ToList();
            string odd = g[baseB + r.Next(6), lang];
            var shown = same.Concat(new[] { odd }).OrderBy(x => r.Next()).ToArray();
            return Build(T("Które słowo nie pasuje do pozostałych? ", "Which word does not belong? ") + string.Join(", ", shown), odd, same, r);
        }

        static Question Logic(int d, Random r)
        {
            switch (d)
            {
                case 1:
                    switch (r.Next(3))
                    {
                        case 0: { int s = r.Next(1, 21), st = r.Next(2, 6); return Sequence(new[] { s, s + st, s + 2 * st, s + 3 * st }, s + 4 * st, r); }
                        case 1: { int s = r.Next(20, 41), st = r.Next(2, 4); return Sequence(new[] { s, s - st, s - 2 * st, s - 3 * st }, s - 4 * st, r); }
                        default: return OddWord(false, r);
                    }
                case 2:
                    switch (r.Next(4))
                    {
                        case 0: { int st = r.Next(2) == 0 ? 5 : 10, s = r.Next(1, 10); return Sequence(new[] { s, s + st, s + 2 * st, s + 3 * st }, s + 4 * st, r); }
                        case 1: { int s = r.Next(40, 61), st = r.Next(3, 8); return Sequence(new[] { s, s - st, s - 2 * st, s - 3 * st }, s - 4 * st, r); }
                        case 2: return MoreRiddle(r);
                        default: return OddWord(r.Next(3) == 0, r);
                    }
                case 3:
                    switch (r.Next(5))
                    {
                        case 0: { int s = r.Next(1, 6); return Sequence(new[] { s, s * 2, s * 4, s * 8 }, s * 16, r, s * 10); }
                        case 1: { int s = r.Next(2, 10), a = r.Next(3, 8), b = r.Next(1, a); return Sequence(new[] { s, s + a, s + a - b, s + 2 * a - b, s + 2 * a - 2 * b }, s + 3 * a - 2 * b, r, s + 2 * a - 3 * b); }
                        case 2: return NotDivisible(r.Next(2, 6), r);
                        case 3: return LegsRiddle(false, r);
                        default: return r.Next(2) == 0 ? OddWord(true, r) : FromList(LogicList, 3, r);
                    }
                case 4:
                    switch (r.Next(5))
                    {
                        case 0: { int s = r.Next(1, 11); return Sequence(new[] { s, s + 1, s + 3, s + 6, s + 10 }, s + 15, r, s + 14); }
                        case 1: { int s = r.Next(1, 3); return Sequence(new[] { s, s * 3, s * 9, s * 27 }, s * 81, r, s * 54); }
                        case 2: return AgeRiddle(r);
                        case 3: return FromList(LogicList, 4, r);
                        default: return OddWord(true, r);
                    }
                case 5:
                    switch (r.Next(5))
                    {
                        case 0: { int n = r.Next(1, 5); return Sequence(new[] { n * n, (n + 1) * (n + 1), (n + 2) * (n + 2), (n + 3) * (n + 3) }, (n + 4) * (n + 4), r); }
                        case 1: { int s = r.Next(1, 6), t = r.Next(30, 51), p = r.Next(1, 4), q = r.Next(2, 6); return Sequence(new[] { s, t, s + p, t - q, s + 2 * p, t - 2 * q }, s + 3 * p, r, t - 3 * q); }
                        case 2: return DaysRiddle(r.Next(2, 4), r);
                        case 3: return FromList(LogicList, 5, r);
                        default: return NotDivisible(r.Next(6, 10), r);
                    }
                case 6:
                    switch (r.Next(5))
                    {
                        case 0: { int a = r.Next(1, 6), b = r.Next(1, 6); return Sequence(new[] { a, b, a + b, a + 2 * b, 2 * a + 3 * b }, 3 * a + 5 * b, r); }
                        case 1: { int s = r.Next(1, 4); return Sequence(new[] { s, 2 * s + 1, 4 * s + 3, 8 * s + 7 }, 16 * s + 15, r, 16 * s + 14); }
                        case 2: return LegsRiddle(true, r);
                        case 3: return FromList(LogicList, 6, r);
                        default: return WeekdayAfter(r.Next(8, 22), r);
                    }
                case 7:
                    switch (r.Next(5))
                    {
                        case 0: { int n = r.Next(4, 9); return Sequence(new[] { n * n, (n + 1) * (n + 1), (n + 2) * (n + 2), (n + 3) * (n + 3) }, (n + 4) * (n + 4), r); }
                        case 1: { int s = r.Next(1, 4), k = r.Next(1, 4); return Sequence(new[] { s, 2 * s, 2 * s + k, 4 * s + 2 * k, 4 * s + 3 * k }, 8 * s + 6 * k, r, 4 * s + 4 * k); }
                        case 2: { int s = r.Next(1, 11); return Sequence(new[] { s, s + 1, s + 3, s + 7, s + 15 }, s + 31, r, s + 23); }
                        case 3:
                        {
                            int n = r.Next(10, 60);
                            return Num(T($"Suma dwóch kolejnych liczb naturalnych wynosi {2 * n + 1}. Jaka jest większa z tych liczb?",
                                         $"Two consecutive whole numbers add up to {2 * n + 1}. What is the bigger number?"), n + 1, r, "", n);
                        }
                        default: return FromList(LogicList, 7, r);
                    }
                case 8:
                    switch (r.Next(6))
                    {
                        case 0: { int n = r.Next(1, 4); Func<int, int> tri = k => k * (k + 1) / 2; return Sequence(new[] { tri(n), tri(n + 1), tri(n + 2), tri(n + 3), tri(n + 4) }, tri(n + 5), r); }
                        case 1: { int n = r.Next(1, 3); Func<int, int> cube = k => k * k * k; return Sequence(new[] { cube(n), cube(n + 1), cube(n + 2), cube(n + 3) }, cube(n + 4), r, cube(n + 3) + 37); }
                        case 2: { int s = r.Next(3, 13); return Sequence(new[] { s * 16, s * 8, s * 4, s * 2 }, s, r, 0); }
                        case 3: { int n = r.Next(1, 4); return Sequence(new[] { n * n + 1, (n + 1) * (n + 1) + 1, (n + 2) * (n + 2) + 1, (n + 3) * (n + 3) + 1 }, (n + 4) * (n + 4) + 1, r); }
                        case 4: return HeadsLegs(r);
                        default: return FromList(LogicList, 8, r);
                    }
                default:
                    switch (r.Next(6))
                    {
                        case 0:
                        {
                            int[] f = { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144 };
                            int i = r.Next(1, 5);
                            return Sequence(f.Skip(i).Take(5).ToArray(), f[i + 5], r, f[i + 4] * 2);
                        }
                        case 1: { int s = r.Next(2, 4); return Sequence(new[] { s, 3 * s - 1, 9 * s - 4, 27 * s - 13 }, 81 * s - 40, r, 81 * s - 39); }
                        case 2:
                        {
                            int[] p = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
                            int i = r.Next(0, 5);
                            return Num(T("To są kolejne liczby pierwsze. Jaka liczba pierwsza będzie następna?", "These are prime numbers in order. Which prime number comes next?") + "  " +
                                string.Join(", ", p.Skip(i).Take(6).Select(v => v.ToString()).ToArray()) + ", ?", p[i + 6], r, "", p[i + 5] + 2, p[i + 6] + 2);
                        }
                        case 3: return HeadsLegs(r);
                        case 4: return WeekdayAfter(r.Next(30, 101), r);
                        default: return FromList(LogicList, 9, r);
                    }
            }
        }

        static Question MoreRiddle(Random r)
        {
            var names = (Pl ? NamesPl : NamesEn).OrderBy(x => r.Next()).Take(3).ToList();
            bool fewest = r.Next(2) == 0;
            string text = T($"{names[0]} ma więcej cukierków niż {names[1]}, a {names[1]} ma więcej niż {names[2]}. Kto ma " + (fewest ? "najmniej" : "najwięcej") + " cukierków?",
                            $"{names[0]} has more sweets than {names[1]}, and {names[1]} has more than {names[2]}. Who has the " + (fewest ? "fewest" : "most") + " sweets?");
            string ans = fewest ? names[2] : names[0];
            return Build(text, ans, names.Where(n => n != ans), r);
        }

        static Question NotDivisible(int m, Random r)
        {
            var nums = Enumerable.Range(2, 10).OrderBy(x => r.Next()).Take(3).Select(k => k * m).ToList();
            int odd;
            do odd = r.Next(m * 2, m * 12); while (odd % m == 0 || nums.Contains(odd));
            return Build(T($"Która liczba NIE dzieli się przez {m}?", $"Which number can NOT be divided exactly by {m}?"), odd.ToString(), nums.Select(v => v.ToString()), r);
        }

        static Question LegsRiddle(bool spiders, Random r)
        {
            int h = r.Next(2, 7), dg = r.Next(2, 6), s = spiders ? r.Next(2, 4) : 0;
            string text = spiders
                ? T($"W zoo są: strusie ({h}), lamy ({dg}) i pająki w terrarium ({s}). Ile nóg mają razem?",
                    $"At the zoo there are {h} ostriches, {dg} llamas and {s} spiders in a tank. How many legs do they have altogether?")
                : T($"Na podwórku są kury ({h}) i psy ({dg}). Ile nóg mają razem?",
                    $"In the yard there are {h} hens and {dg} dogs. How many legs do they have altogether?");
            int legs = 2 * h + 4 * dg + 8 * s;
            return Num(text, legs, r, "", 4 * (h + dg + s), legs - 2);
        }

        static Question AgeRiddle(Random r)
        {
            int a = r.Next(10, 16), k = r.Next(2, 6), m = r.Next(2, 6), res = a - k + m;
            return Num(T($"Siostra ma {a} {Lat(a)}, a brat jest od niej o {k} {Lat(k)} młodszy. Ile lat będzie miał brat za {m} {Lat(m)}?",
                         $"A sister is {a} years old and her brother is {k} years younger. How old will the brother be in {m} years?"), res, r, "", a + k + m, a - k);
        }

        static Question DaysRiddle(int k, Random r)
        {
            var days = Pl ? DaysPl : DaysEn;
            int future = r.Next(7), today = (future - k + 7) % 7;
            string text = k == 2 ? T($"Pojutrze będzie {days[future]}. Jaki dzień jest dzisiaj?", $"The day after tomorrow will be {days[future]}. What day is it today?")
                                 : T($"Za {k} dni będzie {days[future]}. Jaki dzień jest dzisiaj?", $"In {k} days it will be {days[future]}. What day is it today?");
            return Build(text, days[today], new[] { days[(today + 1) % 7], days[(future + k) % 7], days[(today + 6) % 7] }, r);
        }

        static Question WeekdayAfter(int k, Random r)
        {
            var days = Pl ? DaysPl : DaysEn;
            int today = r.Next(7), res = (today + k) % 7;
            return Build(T($"Dziś jest {days[today]}. Jaki dzień tygodnia będzie za {k} dni?", $"Today is {days[today]}. What day of the week will it be in {k} days?"),
                days[res], new[] { days[(res + 1) % 7], days[(res + 6) % 7], days[(res + 2) % 7] }, r);
        }

        static Question HeadsLegs(Random r)
        {
            int rabbits = r.Next(2, 8), hens = r.Next(2, 9), heads = rabbits + hens, legs = 4 * rabbits + 2 * hens;
            return Num(T($"Na podwórku są kury i króliki. Razem mają głowy: {heads}, a nogi: {legs}. Ile jest królików?",
                         $"In a yard there are hens and rabbits. Together they have {heads} heads and {legs} legs. How many rabbits are there?"), rabbits, r, "", hens);
        }

        static readonly Item[] LogicList =
        {
            Q(3, "Ile końców mają razem 3 patyki?", "How many ends do 3 sticks have altogether?", "6|3|4|9", "6|3|4|9"),
            Q(3, "Ile ścian ma kostka do gry (sześcian)?", "How many faces does a dice (a cube) have?", "6|4|8|12", "6|4|8|12"),
            Q(4, "Jaka jest najmniejsza liczba trzycyfrowa?", "What is the smallest three-digit number?", "100|101|111|999", "100|101|111|999"),
            Q(4, "Jaka jest największa liczba dwucyfrowa?", "What is the largest two-digit number?", "99|100|98|90", "99|100|98|90"),
            Q(4, "Ania jest starsza od Basi. Basia jest starsza od Celiny. Kto jest najstarszy?", "Anna is older than Beth. Beth is older than Clare. Who is the oldest?", "Ania|Basia|Celina", "Anna|Beth|Clare"),
            Q(5, "W wyścigu wyprzedzasz zawodnika, który biegnie na drugim miejscu. Na którym miejscu teraz jesteś?", "In a race you overtake the runner in second place. What place are you in now?", "na drugim|na pierwszym|na trzecim", "second|first|third"),
            Q(5, "Ile kwadratów 1 × 1 zmieści się w kwadracie 4 × 4?", "How many 1 × 1 squares fit inside a 4 × 4 square?", "16|8|12|4", "16|8|12|4"),
            Q(5, "Wszystkie koty mają wąsy. Mruczek jest kotem. Co wiemy na pewno?", "All cats have whiskers. Tiger is a cat. What do we know for sure?", "Mruczek ma wąsy|Mruczek nie ma wąsów|Mruczek jest psem", "Tiger has whiskers|Tiger has no whiskers|Tiger is a dog"),
            Q(6, "Ile razy w ciągu doby obie wskazówki zegara pokazują dokładnie na 12?", "How many times in a day and night do both hands of a clock point exactly at 12?", "2|1|12|24", "2|1|12|24"),
            Q(6, "Ile krawędzi ma sześcian?", "How many edges does a cube have?", "12|6|8|10", "12|6|8|10"),
            Q(6, "Ile wierzchołków ma sześcian?", "How many corners (vertices) does a cube have?", "8|6|12|4", "8|6|12|4"),
            Q(7, "Z ilu małych sześcianów 1 × 1 × 1 składa się sześcian 3 × 3 × 3?", "How many small 1 × 1 × 1 cubes make a 3 × 3 × 3 cube?", "27|9|18|12", "27|9|18|12"),
            Q(7, "Ile jest liczb dwucyfrowych?", "How many two-digit numbers are there?", "90|99|100|89", "90|99|100|89"),
            Q(7, "Ile minut mija między 11:50 a 12:10?", "How many minutes pass between 11:50 and 12:10?", "20|60|40|10", "20|60|40|10"),
            Q(8, "Ile cyfr trzeba napisać, żeby zapisać wszystkie liczby od 1 do 20?", "How many digits do you need to write all the numbers from 1 to 20?", "31|20|29|40", "31|20|29|40"),
            Q(8, "Pomyślałem liczbę, pomnożyłem ją przez 3 i dodałem 4. Wyszło 25. Jaką liczbę pomyślałem?", "I think of a number, multiply it by 3 and add 4. I get 25. What was my number?", "7|9|8|6", "7|9|8|6"),
            Q(8, "Ile jest liczb parzystych od 1 do 50?", "How many even numbers are there from 1 to 50?", "25|24|26|50", "25|24|26|50"),
            Q(9, "4 osoby witają się tak, że każda podaje rękę każdej innej jeden raz. Ile jest wszystkich uścisków dłoni?", "4 people meet and each shakes hands with every other person once. How many handshakes are there?", "6|4|8|12", "6|4|8|12"),
            Q(9, "Kasia ma tyle samo braci, co sióstr. Jej brat ma dwa razy więcej sióstr niż braci. Ile dzieci jest w tej rodzinie?", "Kate has as many brothers as sisters. Her brother has twice as many sisters as brothers. How many children are in the family?", "7|5|6|8", "7|5|6|8"),
            Q(9, "Ile przekątnych ma sześciokąt?", "How many diagonals does a hexagon have?", "9|6|12|3", "9|6|12|3"),
            Q(9, "Pomyślałem liczbę, odjąłem 5 i wynik podzieliłem przez 4. Wyszło 6. Jaką liczbę pomyślałem?", "I think of a number, subtract 5 and divide the result by 4. I get 6. What was my number?", "29|19|24|11", "29|19|24|11"),
        };

        // ------------------------------------------------------------------ letters & words

        /// <summary>Compares words letter by letter in the Polish (ą after a, ł after l, ...) or English alphabet.</summary>
        static int AlphaCompare(string a, string b)
        {
            const string alphabet = "aąbcćdeęfghijklłmnńoópqrsśtuvwxyzźż";
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                int x = alphabet.IndexOf(char.ToLowerInvariant(a[i])), y = alphabet.IndexOf(char.ToLowerInvariant(b[i]));
                if (x != y) return x.CompareTo(y);
            }
            return a.Length.CompareTo(b.Length);
        }

        static readonly string[] AlphaPl = { "kot", "pies", "dom", "las", "ryba", "szkoła", "jabłko", "banan", "zebra", "motyl", "okno", "lampa", "żółw", "nos",
                                             "góra", "chleb", "ul", "woda", "igła", "ekran", "farba", "herbata", "tata", "cebula", "łódka", "ćma", "śnieg", "zamek" };
        static readonly string[] AlphaEn = { "cat", "dog", "sun", "house", "tree", "fish", "bike", "apple", "zebra", "lion", "window", "nose", "owl", "castle",
                                             "lake", "garden", "queen", "rabbit", "umbrella", "violin", "yellow", "jam", "kite", "egg", "pencil", "monkey" };
        static readonly string[][] AlphaGroupsPl =
        {
            new[] { "kot", "koń", "kura", "krowa", "kwiat", "klucz", "kubek", "kino", "kaczka", "kość" },
            new[] { "sowa", "ser", "sanki", "sok", "słoń", "sweter", "sufit", "sałata", "sikorka", "smok" },
            new[] { "mama", "mleko", "motyl", "miś", "małpa", "mucha", "most", "mapa", "medal", "mrówka" },
            new[] { "banan", "but", "brat", "biurko", "balon", "burak", "beczka", "bocian", "bułka", "bóbr" },
            new[] { "pies", "park", "pomidor", "ptak", "piłka", "pudło", "pralka", "pędzel", "półka", "pociąg" },
            new[] { "las", "lis", "lody", "lampa", "lekcja", "łódka", "łyżka", "łąka", "lew", "lot" },
        };
        static readonly string[][] AlphaGroupsEn =
        {
            new[] { "ball", "banana", "bird", "book", "bus", "bread", "bell", "boat", "bear", "brick" },
            new[] { "sun", "school", "snake", "star", "sea", "sock", "sand", "spoon", "salt", "shell" },
            new[] { "cat", "cake", "cow", "cup", "city", "car", "clock", "crab", "camel", "cloud" },
            new[] { "ship", "shop", "shoe", "shell", "sheep", "shark", "shirt", "shadow", "shelf", "show" },
            new[] { "train", "tree", "table", "tiger", "toy", "tent", "tooth", "tomato", "tunnel", "ticket" },
            new[] { "map", "milk", "moon", "mouse", "mug", "mask", "melon", "mirror", "monkey", "music" },
        };

        static Question Alphabet(int d, Random r)
        {
            List<string> words;
            if (d <= 2)
                words = (Pl ? AlphaPl : AlphaEn).GroupBy(x => x[0]).Select(g => g.First()).OrderBy(x => r.Next()).Take(d == 1 ? 3 : 4).ToList();
            else
            {
                var group = Pick(Pl ? AlphaGroupsPl : AlphaGroupsEn, r).AsEnumerable();
                if (d <= 4)
                {
                    // easier: the words differ already at the first letter where the group's words are not all the same
                    int pos = 1;
                    while (pos < 3 && group.Select(x => x.Length > pos ? x[pos] : ' ').Distinct().Count() < 3) pos++;
                    group = group.GroupBy(x => x.Length > pos ? x[pos] : ' ').Select(g => g.First()).ToList();
                }
                words = group.OrderBy(x => r.Next()).Take(d <= 3 ? 3 : 4).ToList();
            }
            var sorted = new List<string>(words);
            sorted.Sort(AlphaCompare);
            bool last = d >= 5 && r.Next(2) == 0;
            string ans = last ? sorted[sorted.Count - 1] : sorted[0];
            string text = last ? T("Które słowo jest ostatnie w kolejności alfabetycznej?", "Which word comes last in alphabetical order?")
                               : T("Które słowo jest pierwsze w kolejności alfabetycznej?", "Which word comes first in alphabetical order?");
            return Build(text, ans, words.Where(x => x != ans), r);
        }

        /// <summary>Spelling: "level|correct|wrong|wrong" (level 1 = d1-3, 2 = d4-6, 3 = d7-9).</summary>
        static readonly string[] SpellPl =
        {
            "1|żaba|rzaba|żapa", "1|rzeka|żeka|rzeca", "1|chleb|hleb|chlep", "1|góra|gura|górra", "1|ołówek|ołuwek|ołówec", "1|chmura|hmura|chmóra",
            "1|ogórek|ogurek|ugórek", "1|mucha|muha|mócha", "1|ucho|uho|ócho", "1|róża|ruża|rórza", "1|żółw|rzółw|żułw", "1|drzewo|dżewo|drzefo",
            "1|ogród|ogrud|ogrót", "1|mrówka|mrufka|mruwka", "1|brzoza|bżoza|brzosa", "1|krzesło|kżesło|krzesuo",
            "2|książka|ksionżka|ksiąszka", "2|wróbel|wrubel|wróbl", "2|herbata|cherbata|herpata", "2|chłopiec|hłopiec|chłopjec", "2|kurczak|kórczak|kurczag",
            "2|łóżko|łużko|łórzko", "2|żyrafa|rzyrafa|żyraffa", "2|hałas|chałas|hałaz", "2|orzech|ożech|orzeh", "2|chomik|homik|chomig",
            "2|hulajnoga|chulajnoga|hulajnoka", "2|marchew|marhew|marchef", "2|jaskółka|jaskułka|jaskółca", "2|wierzba|wieżba|wierzpa", "2|żelazo|rzelazo|żelaso", "2|kożuch|korzuch|kożuh",
            "3|rzodkiewka|żodkiewka|rzodkiefka", "3|żołnierz|rzołnierz|żołnież", "3|historia|chistoria|hystoria", "3|bohater|bochater|bohatyr", "3|chrząszcz|hrząszcz|chżąszcz",
            "3|przyjaciel|pszyjaciel|przyjacziel", "3|stróż|strósz|strórz", "3|wrzesień|wżesień|wrzesien", "3|rzeżucha|żeżucha|rzerzucha", "3|huragan|churagan|hurragan",
            "3|chochla|hochla|chohla", "3|pszczoła|pczoła|pszczuła",
        };
        static readonly string[] SpellEn =
        {
            "1|school|scool|skool", "1|friend|freind|frend", "1|because|becuase|becos", "1|said|sed|sayd", "1|people|peple|poeple", "1|animal|animul|aminal",
            "1|water|watter|wotter", "1|little|littel|litle", "1|night|nite|nigth", "1|could|cud|coud",
            "2|beautiful|beutiful|beautifull", "2|Wednesday|Wensday|Wednsday", "2|February|Febuary|Februery", "2|library|libary|liberry", "2|different|diffrent|diferent",
            "2|tomorrow|tommorow|tomorow", "2|chocolate|choclate|chocolat", "2|minute|minit|minnute", "2|answer|anser|answere", "2|island|iland|ighland",
            "3|necessary|neccessary|necesary", "3|separate|seperate|seprate", "3|believe|beleive|belive", "3|receive|recieve|receeve", "3|address|adress|addres",
            "3|beginning|begining|beggining", "3|rhythm|rythm|rhythym", "3|piece|peice|pece", "3|enough|enuff|enouf", "3|surprise|suprise|serprise", "3|knowledge|knowlege|nowledge",
        };

        static Question Spelling(int d, Random r)
        {
            int level = d <= 3 ? 1 : d <= 6 ? 2 : 3;
            var pool = (Pl ? SpellPl : SpellEn).Select(s => s.Split('|')).Where(p => int.Parse(p[0]) == level).ToList();
            var item = Pick(pool, r);
            return Build(T("Które słowo jest napisane poprawnie?", "Which word is spelled correctly?"), item[1], item.Skip(2), r);
        }

        static readonly string[][] PartsPl =
        {
            new[] { "kot", "dom", "szkoła", "drzewo", "książka", "rower", "jabłko", "chmura", "rzeka", "ołówek", "zamek", "żółw" },
            new[] { "biegać", "czytać", "pisać", "śpiewać", "skakać", "jeść", "pływać", "rysować", "spać", "grać", "tańczyć", "liczyć" },
            new[] { "duży", "wesoły", "zielony", "szybki", "mądry", "ciepły", "kolorowy", "wysoki", "smutny", "cichy", "leniwy", "odważny" },
            new[] { "szybko", "wesoło", "głośno", "cicho", "wolno", "dobrze", "pięknie", "wysoko", "nisko", "często", "leniwie", "mądrze" },
        };
        static readonly string[][] PartsEn =
        {
            new[] { "apple", "river", "pencil", "teacher", "elephant", "window", "mountain", "banana", "kitchen", "turtle", "castle", "umbrella" },
            new[] { "write", "sing", "eat", "forget", "bring", "choose", "speak", "listen", "think", "grow", "explain", "carry" },
            new[] { "happy", "tall", "beautiful", "small", "funny", "hungry", "noisy", "angry", "careful", "lazy", "shiny", "polite" },
            new[] { "quickly", "slowly", "loudly", "happily", "quietly", "carefully", "badly", "softly", "safely", "sadly", "lazily", "politely" },
        };
        static readonly string[] PartNamesPl = { "rzeczownikiem", "czasownikiem", "przymiotnikiem", "przysłówkiem" };
        static readonly string[] PartNamesEn = { "a noun", "a verb", "an adjective", "an adverb" };

        static Question PartOfSpeech(int d, Random r)
        {
            int classes = d <= 2 ? 2 : d <= 4 ? 3 : 4;
            int target = r.Next(classes);
            var parts = Pl ? PartsPl : PartsEn;
            string ans = Pick(parts[target], r);
            var wrong = Enumerable.Range(0, classes).Where(k => k != target)
                .SelectMany(k => parts[k].OrderBy(x => r.Next()).Take(classes == 2 ? 2 : 1)).ToList();
            string text = Pl ? $"Które słowo jest {PartNamesPl[target]}?" : $"Which word is {PartNamesEn[target]}?";
            return Build(text, ans, wrong, r);
        }

        static readonly string[,] Opposites =
        {
            { "duży", "mały", "big", "small" }, { "wysoki", "niski", "tall", "short" }, { "gorący", "zimny", "hot", "cold" },
            { "szybki", "wolny", "fast", "slow" }, { "dzień", "noc", "day", "night" }, { "wesoły", "smutny", "happy", "sad" },
            { "stary", "młody", "old", "young" }, { "ciężki", "lekki", "heavy", "light" }, { "pełny", "pusty", "full", "empty" },
            { "czysty", "brudny", "clean", "dirty" }, { "otwarty", "zamknięty", "open", "closed" }, { "góra", "dół", "up", "down" },
        };
        static readonly string[,] OppositesHard =
        {
            { "odważny", "tchórzliwy", "brave", "cowardly" }, { "hałaśliwy", "cichy", "noisy", "quiet" }, { "wcześnie", "późno", "early", "late" },
            { "szeroki", "wąski", "wide", "narrow" }, { "głęboki", "płytki", "deep", "shallow" }, { "mokry", "suchy", "wet", "dry" },
            { "twardy", "miękki", "hard", "soft" }, { "kupować", "sprzedawać", "buy", "sell" }, { "pamiętać", "zapominać", "remember", "forget" },
            { "wygrywać", "przegrywać", "win", "lose" }, { "bogaty", "biedny", "rich", "poor" }, { "jasny", "ciemny", "light", "dark" },
        };

        static Question Opposite(string[,] table, bool englishWords, Random r)
        {
            int i = r.Next(table.GetLength(0));
            int col = englishWords || !Pl ? 2 : 0;
            string a = table[i, col], b = table[i, col + 1];
            var wrong = Enumerable.Range(0, table.GetLength(0)).Where(k => k != i).OrderBy(k => r.Next()).Take(3).Select(k => table[k, col + 1]);
            string text = englishWords ? $"Jakie jest przeciwieństwo angielskiego słowa „{a}”?"
                                       : T($"Co jest przeciwieństwem słowa „{a}”?", $"What is the opposite of \"{a}\"?");
            return Build(text, b, wrong, r);
        }

        /// <summary>Synonyms: "word|synonym|wrong|wrong".</summary>
        static readonly string[] SynonymsPl =
        {
            "wesoły|radosny|smutny|zielony", "duży|wielki|mały|cichy", "szybki|prędki|powolny|kwaśny", "ładny|piękny|brzydki|głośny", "mądry|inteligentny|głupi|mokry",
            "odważny|dzielny|tchórzliwy|głodny", "zmęczony|wyczerpany|wypoczęty|kolorowy", "samochód|auto|rower|ulica", "zaczynać|rozpoczynać|kończyć|spać",
            "smutny|przygnębiony|wesoły|szybki", "mały|niewielki|ogromny|ciepły", "patrzeć|spoglądać|słuchać|biegać",
        };
        static readonly string[] SynonymsEn =
        {
            "happy|glad|sad|green", "big|large|tiny|quiet", "fast|quick|slow|wet", "begin|start|finish|sleep", "little|small|huge|loud", "clever|smart|silly|cold",
            "scared|afraid|brave|hungry", "shut|close|open|eat", "difficult|hard|easy|round", "finish|end|start|jump", "sick|ill|healthy|blue", "rich|wealthy|poor|kind",
        };

        static Question Synonym(Random r)
        {
            var p = Pick(Pl ? SynonymsPl : SynonymsEn, r).Split('|');
            return Build(T($"Które słowo znaczy prawie to samo co „{p[0]}”?", $"Which word means almost the same as \"{p[0]}\"?"), p[1], p.Skip(2), r);
        }

        /// <summary>Plurals: "level|singular|plural|wrong|wrong" (level 1 for d3-5, 2 for d6+).</summary>
        static readonly string[] PluralsPl =
        {
            "1|pies|psy|piesy|piesie", "1|dziecko|dzieci|dziecka|dzieckowie", "1|człowiek|ludzie|człowieki|człowiekowie", "1|ręka|ręce|rękie|rąki",
            "1|koń|konie|koniy|koniowie", "1|ząb|zęby|ząby|zębowie",
            "2|oko|oczy|oki|ocze", "2|ucho|uszy|uchy|usze", "2|brat|bracia|braty|bratcy", "2|tydzień|tygodnie|tydzienie|tydnie", "2|rok|lata|rokowie|rocy",
            "2|przyjaciel|przyjaciele|przyjacielowie|przyjaciely",
        };
        static readonly string[] PluralsEn =
        {
            "1|box|boxes|boxs|boxies", "1|baby|babies|babys|babyes", "1|bus|buses|buss|busies", "1|city|cities|citys|cityes", "1|man|men|mans|mens",
            "1|woman|women|womans|womens", "1|child|children|childs|childrens",
            "2|mouse|mice|mouses|mices", "2|foot|feet|foots|feets", "2|tooth|teeth|tooths|teeths", "2|goose|geese|gooses|geeses", "2|sheep|sheep|sheeps|sheepes",
            "2|wolf|wolves|wolfs|wolfes", "2|potato|potatoes|potatos|potatoe", "2|knife|knives|knifs|knive", "2|leaf|leaves|leafes|leavs",
        };

        static Question Plural(int d, bool english, Random r)
        {
            int level = d <= 5 ? 1 : 2;
            var pool = ((english || !Pl) ? PluralsEn : PluralsPl).Select(s => s.Split('|')).Where(p => int.Parse(p[0]) <= level).ToList();
            var p2 = Pick(pool, r);
            string text = english ? $"Jak brzmi liczba mnoga angielskiego słowa „{p2[1]}”?"
                                  : T($"Jak brzmi liczba mnoga słowa „{p2[1]}”?", $"What is the plural of \"{p2[1]}\"?");
            return Build(text, p2[2], p2.Skip(3), r);
        }

        static Question Letters(int d, Random r)
        {
            var kinds = new List<int> { 0, 1, 2, 7 };
            if (d <= 4) kinds.Add(3);
            if (d >= 3) kinds.Add(4);
            if (d >= 4) kinds.Add(5);
            if (d >= 5) kinds.Add(6);
            switch (Pick(kinds, r))
            {
                case 0: return Spelling(d, r);
                case 1: return Alphabet(d, r);
                case 2: return PartOfSpeech(d, r);
                case 3: return Opposite(Opposites, false, r);
                case 4: return Plural(d, false, r);
                case 5: return Synonym(r);
                case 6: return Opposite(OppositesHard, false, r);
                default: return FromList(Pl ? LettersPl : LettersEn, d, r);
            }
        }

        static Item QL(int d, string text, string answers) => Q(d, text, text, answers, answers);

        static readonly Item[] LettersPl =
        {
            QL(1, "Jaki znak stawiamy na końcu zdania pytającego?", "?|.|,"),
            QL(1, "Ile sylab ma słowo „samochód”?", "3|2|4"),
            QL(2, "Które słowo piszemy wielką literą?", "Kórnik|szkoła|jezioro"),
            QL(2, "Ile sylab ma słowo „lokomotywa”?", "5|4|3"),
            QL(2, "Które słowo jest zdrobnieniem od „kot”?", "kotek|kocur|kocisko"),
            QL(3, "Na jakie pytania odpowiada rzeczownik?", "kto? co?|co robi?|jaki? jaka? jakie?"),
            QL(3, "Na jakie pytanie odpowiada czasownik?", "co robi?|kto? co?|jaki? jaka? jakie?"),
            QL(3, "Które słowo piszemy wielką literą?", "Poznań|miasto|rzeka"),
            QL(4, "Na jakie pytania odpowiada przymiotnik?", "jaki? jaka? jakie?|kto? co?|co robi?"),
            QL(4, "Kto napisał wiersz „Lokomotywa”?", "Julian Tuwim|Jan Brzechwa|Adam Mickiewicz"),
            QL(4, "Kto napisał wiersz „Na straganie”?", "Jan Brzechwa|Julian Tuwim|Maria Konopnicka"),
            QL(4, "Jaki rodzaj ma rzeczownik „książka”?", "żeński|męski|nijaki"),
            QL(5, "Na jakie pytanie odpowiada przysłówek?", "jak?|kto? co?|jaki? jaka? jakie?"),
            QL(5, "Ile liter ma polski alfabet?", "32|26|24|35"),
            QL(5, "Które słowo piszemy małą literą?", "poniedziałek|Polska|Warta"),
            QL(5, "Kto napisał „Akademię pana Kleksa”?", "Jan Brzechwa|Julian Tuwim|Henryk Sienkiewicz"),
            QL(5, "W jakim czasie jest czasownik „pisałem”?", "w przeszłym|w teraźniejszym|w przyszłym"),
            QL(6, "Jaki rodzaj ma rzeczownik „okno”?", "nijaki|męski|żeński"),
            QL(6, "Jaką częścią mowy jest słowo „pięć”?", "liczebnikiem|przymiotnikiem|rzeczownikiem"),
            QL(6, "W jakim czasie jest „będę czytać”?", "w przyszłym|w przeszłym|w teraźniejszym"),
            QL(6, "Kto napisał „Dzieci z Bullerbyn”?", "Astrid Lindgren|Hans Christian Andersen|Jan Brzechwa"),
            QL(6, "Jak nazywa się część zdania, która mówi, co robi podmiot?", "orzeczenie|przydawka|okolicznik"),
            QL(7, "Jaką częścią mowy jest słowo „on”?", "zaimkiem|rzeczownikiem|czasownikiem"),
            QL(7, "Jak nazywa się część zdania, która mówi, kto lub co wykonuje czynność?", "podmiot|orzeczenie|okolicznik"),
            QL(7, "Jak nazywamy wyrazy o przeciwnym znaczeniu, np. „duży – mały”?", "wyrazy przeciwstawne|wyrazy bliskoznaczne|wyrazy pokrewne"),
            QL(7, "Kto napisał powieść „W pustyni i w puszczy”?", "Henryk Sienkiewicz|Adam Mickiewicz|Bolesław Prus"),
            QL(7, "Kto napisał „Kubusia Puchatka”?", "A. A. Milne|Astrid Lindgren|Carlo Collodi"),
            QL(8, "Jak nazywamy wyrazy o podobnym znaczeniu, np. „mądry – inteligentny”?", "wyrazy bliskoznaczne|wyrazy przeciwstawne|wyrazy dźwiękonaśladowcze"),
            QL(8, "Jaką częścią mowy jest słowo „i” w zdaniu „kot i pies”?", "spójnikiem|przyimkiem|zaimkiem"),
            QL(8, "Jaką częścią mowy jest słowo „na” w wyrażeniu „na stole”?", "przyimkiem|spójnikiem|przysłówkiem"),
            QL(8, "Kto napisał „Pana Tadeusza”?", "Adam Mickiewicz|Henryk Sienkiewicz|Jan Brzechwa"),
            QL(8, "Jak nazywamy wyrazy z tej samej rodziny, np. „las, leśny, leśniczy”?", "wyrazy pokrewne|wyrazy przeciwstawne|wyrazy bliskoznaczne"),
            QL(9, "Które zdanie jest zdaniem wykrzyknikowym?", "Ale piękny zamek!|Czy to jest zamek?|To jest zamek."),
            QL(9, "Jak nazywamy wyrazy naśladujące dźwięki, np. „kukuryku”, „bęc”?", "wyrazy dźwiękonaśladowcze|wyrazy pokrewne|wyrazy bliskoznaczne"),
            QL(9, "W jakim przypadku jest rzeczownik w zdaniu „Nie mam kota”?", "w dopełniaczu|w mianowniku|w bierniku"),
            QL(9, "Jak nazywa się przypadek, który odpowiada na pytania „kto? co?”?", "mianownik|dopełniacz|celownik"),
            QL(9, "Kto napisał „Pinokia”?", "Carlo Collodi|Hans Christian Andersen|Bracia Grimm"),
        };

        static readonly Item[] LettersEn =
        {
            QL(1, "Which punctuation mark goes at the end of a question?", "?|.|,"),
            QL(1, "How many syllables are in the word \"banana\"?", "3|2|4"),
            QL(2, "Which word needs a capital letter?", "London|city|river"),
            QL(2, "How many syllables are in the word \"elephant\"?", "3|2|4"),
            QL(2, "Which word rhymes with \"cat\"?", "hat|cot|cup"),
            QL(3, "Which part of speech is a \"doing\" word, like run or eat?", "a verb|a noun|an adjective"),
            QL(3, "Which part of speech names a person, place or thing?", "a noun|a verb|an adverb"),
            QL(3, "Which word needs a capital letter?", "Monday|morning|week"),
            QL(4, "Which part of speech describes a noun, like big or red?", "an adjective|a verb|an adverb"),
            QL(4, "How many letters are in the English alphabet?", "26|24|28|32"),
            QL(4, "What is the past tense of \"go\"?", "went|goed|goes"),
            QL(4, "How many syllables are in the word \"caterpillar\"?", "4|3|5"),
            QL(5, "Which part of speech tells us how something is done, like slowly?", "an adverb|a noun|an adjective"),
            QL(5, "What is the past tense of \"run\"?", "ran|runned|runs"),
            QL(5, "Which is the short form of \"do not\"?", "don't|doesn't|didn't"),
            QL(5, "Who wrote the Harry Potter books?", "J. K. Rowling|Roald Dahl|A. A. Milne"),
            QL(6, "Which word is a pronoun?", "she|table|run"),
            QL(6, "Which sentence is correct?", "She doesn't like milk.|She don't like milk.|She not like milk."),
            QL(6, "What is the past tense of \"swim\"?", "swam|swimmed|swims"),
            QL(6, "Who wrote \"Winnie-the-Pooh\"?", "A. A. Milne|Roald Dahl|Lewis Carroll"),
            QL(7, "Which word is a conjunction (a joining word)?", "and|under|happy"),
            QL(7, "What is the past tense of \"think\"?", "thought|thinked|thank"),
            QL(7, "What do we call words with opposite meanings, like hot and cold?", "antonyms|synonyms|homophones"),
            QL(7, "Who wrote \"Charlie and the Chocolate Factory\"?", "Roald Dahl|A. A. Milne|Lewis Carroll"),
            QL(7, "What is the comparative of \"good\"?", "better|gooder|best"),
            QL(8, "In \"The cat is under the table\", which word is a preposition?", "under|cat|table"),
            QL(8, "What do we call words with similar meanings, like big and large?", "synonyms|antonyms|homophones"),
            QL(8, "What is the past tense of \"catch\"?", "caught|catched|catches"),
            QL(8, "Who wrote \"Alice's Adventures in Wonderland\"?", "Lewis Carroll|Roald Dahl|J. K. Rowling"),
            QL(9, "What do we call words that sound the same but are spelled differently, like \"sea\" and \"see\"?", "homophones|synonyms|antonyms"),
            QL(9, "Which sentence is an exclamation?", "What a beautiful castle!|Is that a castle?|That is a castle."),
            QL(9, "Which word is an adverb in \"The turtle walked slowly home\"?", "slowly|turtle|walked"),
            QL(9, "What is the superlative of \"bad\"?", "worst|baddest|worse"),
        };

        // ------------------------------------------------------------------ English words (Polish words for English players)

        /// <summary>Vocabulary: { Polish, English, level 1..9 }.</summary>
        static readonly string[,] Dictionary =
        {
            { "pies", "dog", "1" }, { "kot", "cat", "1" }, { "koń", "horse", "1" }, { "krowa", "cow", "1" }, { "ptak", "bird", "1" }, { "ryba", "fish", "1" },
            { "czerwony", "red", "1" }, { "niebieski", "blue", "1" }, { "zielony", "green", "1" }, { "żółty", "yellow", "1" }, { "mama", "mum", "1" },
            { "tata", "dad", "1" }, { "siostra", "sister", "1" }, { "brat", "brother", "1" }, { "dom", "house", "1" }, { "dziesięć", "ten", "1" },
            { "jabłko", "apple", "2" }, { "chleb", "bread", "2" }, { "mleko", "milk", "2" }, { "woda", "water", "2" }, { "ser", "cheese", "2" }, { "jajko", "egg", "2" },
            { "książka", "book", "2" }, { "ołówek", "pencil", "2" }, { "szkoła", "school", "2" }, { "nauczyciel", "teacher", "2" }, { "krzesło", "chair", "2" },
            { "stół", "table", "2" }, { "banan", "banana", "2" }, { "sok", "juice", "2" },
            { "głowa", "head", "3" }, { "ręka", "hand", "3" }, { "oko", "eye", "3" }, { "ucho", "ear", "3" }, { "nos", "nose", "3" }, { "noga", "leg", "3" },
            { "słońce", "sun", "3" }, { "deszcz", "rain", "3" }, { "śnieg", "snow", "3" }, { "wiatr", "wind", "3" }, { "chmura", "cloud", "3" },
            { "poniedziałek", "Monday", "3" }, { "piątek", "Friday", "3" }, { "niedziela", "Sunday", "3" },
            { "styczeń", "January", "4" }, { "marzec", "March", "4" }, { "lipiec", "July", "4" }, { "grudzień", "December", "4" }, { "wiosna", "spring", "4" },
            { "lato", "summer", "4" }, { "jesień", "autumn", "4" }, { "zima", "winter", "4" }, { "kuchnia", "kitchen", "4" }, { "sypialnia", "bedroom", "4" },
            { "ogród", "garden", "4" }, { "okno", "window", "4" }, { "drzwi", "door", "4" }, { "łazienka", "bathroom", "4" },
            { "lekarz", "doctor", "5" }, { "rolnik", "farmer", "5" }, { "strażak", "firefighter", "5" }, { "policjant", "police officer", "5" }, { "kucharz", "cook", "5" },
            { "biegać", "run", "5" }, { "pływać", "swim", "5" }, { "czytać", "read", "5" }, { "pisać", "write", "5" }, { "śpiewać", "sing", "5" },
            { "spać", "sleep", "5" }, { "jeść", "eat", "5" }, { "pić", "drink", "5" }, { "tańczyć", "dance", "5" },
            { "zmęczony", "tired", "6" }, { "głodny", "hungry", "6" }, { "odważny", "brave", "6" }, { "biblioteka", "library", "6" }, { "szpital", "hospital", "6" },
            { "sklep", "shop", "6" }, { "most", "bridge", "6" }, { "zamek", "castle", "6" }, { "jezioro", "lake", "6" }, { "las", "forest", "6" },
            { "góra", "mountain", "6" }, { "rzeka", "river", "6" }, { "wieś", "village", "6" }, { "ulica", "street", "6" },
            { "kurtka", "jacket", "7" }, { "kapelusz", "hat", "7" }, { "spodnie", "trousers", "7" }, { "sukienka", "dress", "7" }, { "szalik", "scarf", "7" },
            { "rękawiczki", "gloves", "7" }, { "pociąg", "train", "7" }, { "samolot", "plane", "7" }, { "statek", "ship", "7" }, { "rower", "bike", "7" },
            { "motyl", "butterfly", "7" }, { "żółw", "turtle", "7" }, { "wiewiórka", "squirrel", "7" }, { "sowa", "owl", "7" },
            { "zawsze", "always", "8" }, { "nigdy", "never", "8" }, { "czasami", "sometimes", "8" }, { "wczoraj", "yesterday", "8" }, { "jutro", "tomorrow", "8" },
            { "dzisiaj", "today", "8" }, { "piękny", "beautiful", "8" }, { "trudny", "difficult", "8" }, { "łatwy", "easy", "8" }, { "cichy", "quiet", "8" },
            { "niebezpieczny", "dangerous", "8" }, { "przyjaźń", "friendship", "8" }, { "sąsiad", "neighbour", "8" }, { "pogoda", "weather", "8" },
            { "środowisko", "environment", "9" }, { "wiedza", "knowledge", "9" }, { "podróż", "journey", "9" }, { "zdrowie", "health", "9" }, { "język", "language", "9" },
            { "wynalazek", "invention", "9" }, { "stulecie", "century", "9" }, { "uczciwy", "honest", "9" }, { "ostrożny", "careful", "9" }, { "chociaż", "although", "9" },
            { "pożyczać", "borrow", "9" }, { "pamiętać", "remember", "9" }, { "zapominać", "forget", "9" }, { "wyjaśniać", "explain", "9" },
        };

        static Question Vocabulary(int d, Random r)
        {
            int n = Dictionary.GetLength(0);
            var pool = Enumerable.Range(0, n).Where(i => int.Parse(Dictionary[i, 2]) == d).ToList();
            int k = Pick(pool, r);
            bool forward = r.Next(3) != 0;      // mostly from the player's language into the other one
            int mine = Pl ? 0 : 1, from = forward ? mine : 1 - mine, to = 1 - from;
            // words of the same level are listed by topic, so near neighbours make the most plausible wrong answers
            var wrong = Enumerable.Range(0, n).Where(i => i != k).OrderBy(i => Math.Abs(int.Parse(Dictionary[i, 2]) - d) * 1000 + Math.Abs(i - k) * 2 + r.Next(9)).Take(3).Select(i => Dictionary[i, to]);
            string word = Dictionary[k, from];
            string text = Pl ? (from == 0 ? $"Jak po angielsku powiesz „{word}”?" : $"Co znaczy angielskie słowo „{word}”?")
                             : (from == 1 ? $"How do you say \"{word}\" in Polish?" : $"What does the Polish word \"{word}\" mean?");
            return Build(text, Dictionary[k, to], wrong, r);
        }

        /// <summary>Irregular past tense for "Yesterday I ___": { verb, past, wrong regular form, rest of the sentence }.</summary>
        static readonly string[,] PastTense =
        {
            { "go", "went", "goed", "to the park" }, { "see", "saw", "seed", "a big dog" }, { "eat", "ate", "eated", "an apple" }, { "buy", "bought", "buyed", "a new ball" },
            { "swim", "swam", "swimmed", "in the lake" }, { "write", "wrote", "writed", "a letter" }, { "come", "came", "comed", "home late" }, { "drink", "drank", "drinked", "some milk" },
            { "take", "took", "taked", "a photo" }, { "make", "made", "maked", "a cake" }, { "run", "ran", "runned", "to school" }, { "sleep", "slept", "sleeped", "for ten hours" },
            { "find", "found", "finded", "a coin" }, { "give", "gave", "gived", "Mum a flower" }, { "have", "had", "haved", "a party" },
        };

        static Question Words(int d, Random r)
        {
            if (!Pl || d <= 2 || r.Next(5) < 3) return Vocabulary(d, r);
            switch (r.Next(4))
            {
                case 0:
                    if (d >= 5)
                    {
                        int i = r.Next(PastTense.GetLength(0));
                        string verb = PastTense[i, 0], past = PastTense[i, 1];
                        string third = verb == "have" ? "has" : verb == "go" ? "goes" : verb + "s";
                        return Build($"Uzupełnij zdanie: Yesterday I ___ ({verb}) {PastTense[i, 3]}.", past, new[] { PastTense[i, 2], third }, r);
                    }
                    return Opposite(Opposites, true, r);
                case 1: return d >= 4 ? Plural(d, true, r) : Opposite(Opposites, true, r);
                case 2: return d >= 6 ? Opposite(OppositesHard, true, r) : Opposite(Opposites, true, r);
                default: return FromList(EnglishPl, d, r);
            }
        }

        /// <summary>English grammar and phrases for Polish players (from difficulty 3).</summary>
        static readonly Item[] EnglishPl =
        {
            QL(3, "Uzupełnij: I ___ eight years old.", "am|is|are"),
            QL(3, "Uzupełnij: They ___ my friends.", "are|is|am"),
            QL(3, "Jak po angielsku powiesz „Dziękuję”?", "Thank you|Please|Sorry"),
            QL(3, "Jak po angielsku powiesz „Dobranoc”?", "Good night|Good morning|Goodbye"),
            QL(3, "Jak po angielsku powiesz „Do widzenia”?", "Goodbye|Hello|Thank you"),
            QL(3, "Uzupełnij: This ___ my dog.", "is|are|am"),
            QL(4, "Uzupełnij: She ___ a cat.", "has|have|is"),
            QL(4, "Uzupełnij: We ___ a big dog.", "have|has|is"),
            QL(4, "Jak zapytasz po angielsku „Jak masz na imię?”", "What's your name?|How old are you?|Where are you from?"),
            QL(4, "Jak po angielsku powiesz „Dzień dobry” rano?", "Good morning|Good night|Goodbye"),
            QL(4, "Która forma jest poprawna?", "an apple|a apple|an apples"),
            QL(4, "Uzupełnij: He ___ ten years old.", "is|are|am"),
            QL(5, "Jak zapytasz po angielsku „Ile masz lat?”", "How old are you?|How are you?|What's your name?"),
            QL(5, "Jak po angielsku powiesz „Mam dziesięć lat”?", "I am ten years old.|I have ten years old.|I am ten year old."),
            QL(5, "Uzupełnij: He ___ football every Saturday.", "plays|play|playing"),
            QL(5, "Uzupełnij: There ___ three cats in the garden.", "are|is|am"),
            QL(5, "Jak po angielsku powiesz „Lubię lody”?", "I like ice cream.|I likes ice cream.|I liking ice cream."),
            QL(5, "Uzupełnij: There ___ a book on the desk.", "is|are|be"),
            QL(6, "Jak zapytasz po angielsku „Która godzina?”", "What time is it?|How old is it?|Where is it?"),
            QL(6, "Jak zapytasz po angielsku „Gdzie mieszkasz?”", "Where do you live?|Where you live?|Where are you live?"),
            QL(6, "Uzupełnij: She ___ like milk.", "doesn't|don't|isn't"),
            QL(6, "Uzupełnij: ___ you like pizza?", "Do|Does|Are"),
            QL(6, "Jak po angielsku powiesz „Powodzenia!”?", "Good luck!|Well done!|Happy birthday!"),
            QL(6, "Uzupełnij: I can ___ very fast.", "run|runs|running"),
            QL(7, "Jaki jest stopień wyższy angielskiego przymiotnika „big”?", "bigger|biger|more big"),
            QL(7, "Jaki jest stopień wyższy angielskiego przymiotnika „good”?", "better|gooder|more good"),
            QL(7, "Uzupełnij: Look! The children ___ in the park.", "are playing|is playing|plays"),
            QL(7, "Uzupełnij: Tom and Anna ___ at school yesterday.", "were|was|are"),
            QL(7, "Uzupełnij: My birthday is ___ May.", "in|on|at"),
            QL(7, "Uzupełnij: I ___ TV right now.", "am watching|watching|watches"),
            QL(8, "Jaki jest stopień najwyższy angielskiego przymiotnika „bad”?", "the worst|the baddest|the worse"),
            QL(8, "Uzupełnij: How ___ apples are there?", "many|much|lot"),
            QL(8, "Uzupełnij: How ___ water is in the bottle?", "much|many|lot"),
            QL(8, "Uzupełnij: The lesson starts ___ 8 o'clock.", "at|in|on"),
            QL(8, "Uzupełnij: She is ___ than her brother.", "taller|tallest|more tall"),
            QL(8, "Uzupełnij: Yesterday I ___ not at home.", "was|were|am"),
            QL(9, "Uzupełnij: We go to school ___ Monday.", "on|in|at"),
            QL(9, "Uzupełnij: If it rains tomorrow, we ___ at home.", "will stay|stays|staying"),
            QL(9, "Jak po angielsku powiesz „Wszystkiego najlepszego z okazji urodzin!”?", "Happy birthday!|Merry Christmas!|Good luck!"),
            QL(9, "Uzupełnij: ___ you play the piano when you were six?", "Did|Do|Were"),
            QL(9, "Uzupełnij: This is the ___ day of my life!", "best|better|goodest"),
            QL(9, "Uzupełnij: There isn't ___ milk in the fridge.", "any|some|many"),
        };

        // ------------------------------------------------------------------ time & calendar

        static readonly string[] DaysPl = { "poniedziałek", "wtorek", "środa", "czwartek", "piątek", "sobota", "niedziela" };
        static readonly string[] DaysEn = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        static readonly string[] MonthsPl = { "styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec", "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień" };
        static readonly string[] MonthsGenPl = { "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca", "lipca", "sierpnia", "września", "października", "listopada", "grudnia" };
        static readonly string[] MonthsEn = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        static readonly string[] HourPl = { "", "pierwsza", "druga", "trzecia", "czwarta", "piąta", "szósta", "siódma", "ósma", "dziewiąta", "dziesiąta", "jedenasta", "dwunasta" };

        static string Date(int day, int month) => day + " " + (Pl ? MonthsGenPl[month] : MonthsEn[month]);

        static void AddDays(ref int day, ref int month, int k)
        {
            day += k;
            while (day > MonthDays[month]) { day -= MonthDays[month]; month = (month + 1) % 12; }
        }

        static Question Time(int d, Random r)
        {
            var days = Pl ? DaysPl : DaysEn;
            var months = Pl ? MonthsPl : MonthsEn;
            int i = r.Next(7), m = r.Next(12);
            int kind = r.Next(d <= 2 ? 4 : 5);
            if (kind == 0) return FromList(TimeList, d, r);
            switch (d)
            {
                case 1:
                    if (kind == 1) return Build(T($"Jaki dzień tygodnia jest po dniu: {days[i]}?", $"Which day comes after {days[i]}?"), days[(i + 1) % 7], days.Where((x, k) => k != (i + 1) % 7).OrderBy(x => r.Next()), r);
                    if (kind == 2) return Build(T($"Jaki miesiąc jest po miesiącu: {months[m]}?", $"Which month comes after {months[m]}?"), months[(m + 1) % 12], months.Where((x, k) => k != (m + 1) % 12).OrderBy(x => r.Next()), r);
                    return HoursLater(r);
                case 2:
                    if (kind == 1) return Build(T($"Jaki dzień tygodnia jest przed dniem: {days[i]}?", $"Which day comes before {days[i]}?"), days[(i + 6) % 7], days.Where((x, k) => k != (i + 6) % 7).OrderBy(x => r.Next()), r);
                    if (kind == 2) return Build(T($"Jaki miesiąc jest przed miesiącem: {months[m]}?", $"Which month comes before {months[m]}?"), months[(m + 11) % 12], months.Where((x, k) => k != (m + 11) % 12).OrderBy(x => r.Next()), r);
                    return MonthNumber(r);
                case 3:
                    if (kind == 1) return DaysInMonth(false, r);
                    if (kind == 2) return MinutesLater(new[] { 30, 15 }, r);
                    if (kind == 3) return MonthNumber(r);
                    return Conversion(false, r);
                case 4:
                    if (kind <= 2) return Clock24(r);
                    if (kind == 3) return Conversion(true, r);
                    return MinutesLater(new[] { 15, 30, 45 }, r);
                case 5:
                    if (kind == 1) return EndTime(r);
                    if (kind == 2) return DaysInMonth(true, r);
                    if (kind == 3) return Conversion(true, r);
                    return Clock24(r);
                case 6:
                    if (kind == 1) return CenturyOf(r);
                    if (kind == 2) return EndTime(r);
                    if (kind == 3) return WeekdayAfter(r.Next(8, 22), r);
                    return DaysInMonth(true, r);
                case 7:
                    if (kind == 1) return DateAfter(r.Next(2, 11), r);
                    if (kind == 2) return CenturyOf(r);
                    if (kind == 3) return MinutesBetween(r);
                    return EndTime(r);
                case 8:
                    if (kind == 1) return LeapYear(r);
                    if (kind == 2) return MinutesBetween(r);
                    if (kind == 3) return WeekdayAfter(r.Next(15, 51), r);
                    return DateAfter(r.Next(5, 16), r);
                default:
                    if (kind == 1) return WeekdayAfter(r.Next(50, 121), r);
                    if (kind == 2) return DateAfter(r.Next(10, 41), r);
                    if (kind == 3) return MinutesBetween(r);
                    return r.Next(2) == 0 ? LeapYear(r) : CenturyOf(r);
            }
        }

        static Question HoursLater(Random r)
        {
            int h = r.Next(6, 16), k = r.Next(2, 6), res = h + k;
            return Build(T($"Jest godzina {h}:00. Która godzina będzie za {k} godz.?", $"It is {h}:00. What time will it be in {k} hours?"),
                res + ":00", new[] { (res + 1) + ":00", (res - 1) + ":00", res + ":30", (h + k + 2) + ":00" }, r);
        }

        static Question MonthNumber(Random r)
        {
            int m = r.Next(12);
            var months = Pl ? MonthsPl : MonthsEn;
            return Build(T($"Którym z kolei miesiącem roku jest {months[m]}?", $"Which month of the year is {months[m]}? (January is 1st)"),
                Pl ? (m + 1) + "." : Ordinal(m + 1), NearInts(m + 1, r).Where(v => v >= 1 && v <= 12).Select(v => Pl ? v + "." : Ordinal(v)), r);
        }

        static Question DaysInMonth(bool withFebruary, Random r)
        {
            int m = r.Next(12);
            if (m == 1 && !withFebruary) m = 2;
            var months = Pl ? MonthsPl : MonthsEn;
            string text = m == 1 ? T("Ile dni ma luty w zwykłym (nieprzestępnym) roku?", "How many days does February have in a normal (not leap) year?")
                                 : T($"Ile dni ma {months[m]}?", $"How many days are there in {months[m]}?");
            return Build(text, MonthDays[m].ToString(), new[] { "30", "31", "28", "29" }, r);
        }

        static Question MinutesLater(int[] steps, Random r)
        {
            int add = Pick(steps, r), start = r.Next(7, 18) * 60 + new[] { 0, 15, 30, 45 }[r.Next(4)], end = start + add;
            string addText = add == 30 ? T("pół godziny", "half an hour") : add == 15 ? T("kwadrans", "a quarter of an hour") : T("trzy kwadranse", "three quarters of an hour");
            return Build(T($"Jest {Hm(start)}. Która godzina będzie za {addText}?", $"It is {Hm(start)}. What time will it be in {addText}?"),
                Hm(end), new[] { Hm(end + 15), Hm(end - 15 == start ? end + 30 : end - 15), Hm(end + 60), Hm(end + 45) }, r);
        }

        static Question Conversion(bool harder, Random r)
        {
            int n = r.Next(2, 5);
            switch (r.Next(harder ? 6 : 3))
            {
                case 0: return Num(T($"Ile sekund mają {n} minuty?", $"How many seconds are there in {n} minutes?"), n * 60, r, "", n * 100);
                case 1: return Num(T($"Ile minut mają {n} godziny?", $"How many minutes are there in {n} hours?"), n * 60, r, "", n * 100);
                case 2: return Num(T($"Ile dni mają {n} tygodnie?", $"How many days are there in {n} weeks?"), n * 7, r, "", n * 5);
                case 3: return Num(T($"Ile godzin mają {n} doby?", $"How many hours are there in {n} whole days?"), n * 24, r, "", n * 12);
                case 4: return Num(T($"Ile to godzin: {n * 60} minut?", $"How many hours are {n * 60} minutes?"), n, r, "", n * 6);
                default: { int s = n * 60 + 30; return Num(T($"Ile sekund to {n} min 30 s?", $"How many seconds are {n} min 30 s?"), s, r, "", n * 100 + 30); }
            }
        }

        static Question Clock24(Random r)
        {
            int h = r.Next(13, 23), h12 = h - 12;
            if (r.Next(2) == 0)
            {
                if (Pl)
                {
                    string part = h <= 17 ? " po południu" : " wieczorem";
                    var wrong = new[] { h12 + 1, h12 - 1, h12 + 2 }.Where(v => v >= 1 && v <= 11).Select(v => HourPl[v] + part).Concat(new[] { HourPl[h12] + " rano" });
                    return Build($"Godzina {h}:00 to inaczej...", HourPl[h12] + part, wrong, r);
                }
                var we = new[] { h12 + 1, h12 - 1, h12 + 2 }.Where(v => v >= 1 && v <= 11).Select(v => v + " p.m.").Concat(new[] { h12 + " a.m." });
                return Build($"{h}:00 is the same time as...", h12 + " p.m.", we, r);
            }
            string ask = Pl ? HourPl[h12] + (h <= 17 ? " po południu" : " wieczorem") : h12 + " p.m.";
            return Build(T($"Którą godzinę na zegarze 24-godzinnym oznacza „{ask}”?", $"What is {ask} on a 24-hour clock?"),
                h + ":00", new[] { h12 + ":00", (h - 2) + ":00", (h + 2) + ":00" }, r);
        }

        static Question EndTime(Random r)
        {
            int start = r.Next(8, 18) * 60 + r.Next(12) * 5, dur = r.Next(5, 31) * 5, end = start + dur;
            string ds = dur >= 60 ? (dur % 60 == 0 ? T($"{dur / 60} godz.", $"{dur / 60} h") : T($"{dur / 60} godz. {dur % 60} min", $"{dur / 60} h {dur % 60} min")) : dur + " min";
            return Build(T($"Film trwający {ds} zaczyna się o {Hm(start)}. O której się skończy?", $"A film that lasts {ds} starts at {Hm(start)}. What time does it end?"),
                Hm(end), new[] { Hm(end + 10), Hm(end - 10), Hm(end + 60), Hm(end - 60 > start ? end - 60 : end + 20) }, r);
        }

        static readonly int[] FamousYears = { 966, 1025, 1410, 1473, 1569, 1683, 1714, 1777, 1791, 1810, 1826, 1918, 2004, 2026 };

        static Question CenturyOf(Random r)
        {
            int y = r.Next(2) == 0 ? Pick(FamousYears, r) : r.Next(101, 2100);
            if (y % 100 == 0) y++;
            int c = (y - 1) / 100 + 1;
            return Build(T($"W którym wieku był rok {y}?", $"Which century was the year {y} in?"), Century(c), new[] { c - 1, c + 1, c + 2 }.Where(v => v >= 1).Select(Century), r);
        }

        static Question DateAfter(int k, Random r)
        {
            int m = r.Next(12), day = MonthDays[m] - r.Next(0, 8);
            int d1 = day, m1 = m;
            AddDays(ref d1, ref m1, k);
            var wrong = new List<string>();
            foreach (int w in new[] { k + 1, k - 1, k + 2 })
            {
                int dw = day, mw = m;
                AddDays(ref dw, ref mw, w);
                wrong.Add(Date(dw, mw));
            }
            string leap = m == 1 || (m == 0 && k > 31 - day) ? T(" (rok nie jest przestępny)", " (not a leap year)") : "";
            return Build(T($"Dziś jest {Date(day, m)}{leap}. Jaka data będzie za {k} dni?", $"Today is {Date(day, m)}{leap}. What will the date be in {k} days?"),
                Date(d1, m1), wrong, r);
        }

        static Question MinutesBetween(Random r)
        {
            int start = r.Next(7, 16) * 60 + r.Next(12) * 5, dur = r.Next(4, 35) * 5, end = start + dur;
            int naive = (end / 60 * 100 + end % 60) - (start / 60 * 100 + start % 60);
            return Num(T($"Wycieczka trwała od {Hm(start)} do {Hm(end)}. Ile minut trwała?", $"A trip lasted from {Hm(start)} to {Hm(end)}. How many minutes did it last?"),
                dur, r, "min", naive, dur + 5);
        }

        static Question LeapYear(Random r)
        {
            int y = 1996 + 4 * r.Next(0, 26);
            return Build(T("Który z tych lat jest (lub był) przestępny?", "Which of these years is a leap year?"), y.ToString(), new[] { y + 1, y + 2, y + 3 }.Select(v => v.ToString()), r);
        }

        // ------------------------------------------------------------------ hand-written lists

        sealed class Item
        {
            public int D;
            public string Pl, En;
            public string[] APl, AEn;   // the first answer is the correct one
            public bool Kornik;         // about Kórnik and our school (the Master Turtle likes these)
        }

        static Item Q(int d, string pl, string en, string apl, string aen) =>
            new Item { D = d, Pl = pl, En = en, APl = apl.Split('|'), AEn = aen.Split('|') };

        static Item QK(int d, string pl, string en, string apl, string aen)
        {
            var i = Q(d, pl, en, apl, aen);
            i.Kornik = true;
            return i;
        }

        static string Key(Item i) => Pl ? i.Pl + "|" + i.APl[0] : i.En + "|" + i.AEn[0];

        /// <summary>
        /// A question from a list: a fresh one of this difficulty if possible, otherwise a fresh one from nearby
        /// difficulties (up to <paramref name="spread"/> levels away), otherwise any nearby one again.
        /// </summary>
        static Question FromList(Item[] list, int d, Random r, int spread = 2)
        {
            Item item = null;
            for (int s = 0; s <= spread && item == null; s++)
            {
                var fresh = list.Where(i => Math.Abs(i.D - d) <= s && !asked.Contains(Key(i))).ToList();
                if (fresh.Count > 0) item = Pick(fresh, r);
            }
            if (item == null)
            {
                var near = list.Where(i => Math.Abs(i.D - d) <= 1).ToList();
                item = Pick(near.Count > 0 ? near : list.ToList(), r);
            }
            var a = Pl ? item.APl : item.AEn;
            return Build(Pl ? item.Pl : item.En, a[0], a.Skip(1), r);
        }

        static readonly Item[] TimeList =
        {
            Q(1, "Który miesiąc jest pierwszy w roku?", "Which is the first month of the year?", "styczeń|grudzień|marzec", "January|December|March"),
            Q(1, "Ile miesięcy ma rok?", "How many months are there in a year?", "12|10|7|24", "12|10|7|24"),
            Q(1, "W którym miesiącu zaczyna się rok szkolny w Polsce?", "In which month does the school year start in Poland?", "we wrześniu|w styczniu|w czerwcu", "September|January|June"),
            Q(1, "Która pora roku przychodzi po zimie?", "Which season comes after winter?", "wiosna|lato|jesień", "spring|summer|autumn"),
            Q(1, "Ile dni ma tydzień?", "How many days are there in a week?", "7|5|10", "7|5|10"),
            Q(2, "Ile minut ma godzina?", "How many minutes are there in an hour?", "60|100|30", "60|100|30"),
            Q(2, "Ile godzin ma doba?", "How many hours are there in a day and night?", "24|12|60", "24|12|60"),
            Q(2, "Ile sekund ma minuta?", "How many seconds are there in a minute?", "60|100|30", "60|100|30"),
            Q(2, "W którym miesiącu jest Wigilia?", "In which month is Christmas Eve?", "w grudniu|w listopadzie|w styczniu", "December|November|January"),
            Q(2, "W którym miesiącu obchodzimy w Polsce Dzień Dziecka?", "In which month is Children's Day celebrated in Poland?", "w czerwcu|w maju|we wrześniu", "June|May|September"),
            Q(2, "Ile dni trwa tydzień szkolny, od poniedziałku do piątku?", "How many days is a school week, from Monday to Friday?", "5|7|6|4", "5|7|6|4"),
            Q(3, "Ile minut ma kwadrans?", "How many minutes are there in a quarter of an hour?", "15|25|45|10", "15|25|45|10"),
            Q(3, "Ile minut ma pół godziny?", "How many minutes are there in half an hour?", "30|15|50", "30|15|50"),
            Q(3, "Ile miesięcy ma pół roku?", "How many months are there in half a year?", "6|5|4", "6|5|4"),
            Q(3, "Ile dni ma luty w zwykłym roku?", "How many days does February have in a normal year?", "28|30|31", "28|30|31"),
            Q(4, "Ile minut mają trzy kwadranse?", "How many minutes are three quarters of an hour?", "45|30|75|35", "45|30|75|35"),
            Q(4, "Ile lat ma stulecie (wiek)?", "How many years are there in a century?", "100|10|1000", "100|10|1000"),
            Q(4, "W którym miesiącu zaczyna się kalendarzowe lato?", "In which month does summer start by the calendar?", "w czerwcu|w maju|w lipcu", "June|May|July"),
            Q(5, "Ile dni ma rok przestępny?", "How many days are there in a leap year?", "366|365|364|360", "366|365|364|360"),
            Q(5, "Który dzień jest w kalendarzu tylko w roku przestępnym?", "Which date is only in the calendar in a leap year?", "29 lutego|1 marca|31 grudnia", "29 February|1 March|31 December"),
            Q(5, "Ile pełnych tygodni ma rok?", "How many full weeks are there in a year?", "52|48|100|12", "52|48|100|12"),
            Q(5, "Co ile lat zwykle wypada rok przestępny?", "How often is there usually a leap year?", "co 4 lata|co 2 lata|co 10 lat", "every 4 years|every 2 years|every 10 years"),
            Q(5, "Ile lat to dekada?", "How many years are there in a decade?", "10|100|12", "10|100|12"),
            Q(5, "W którym wieku żyjemy?", "Which century do we live in?", "w XXI|w XX|w XXII", "the 21st|the 20th|the 22nd"),
            Q(6, "Ile dni mają razem lipiec i sierpień?", "How many days do July and August have together?", "62|61|60", "62|61|60"),
            Q(6, "Ile lat to tysiąclecie?", "How many years are there in a millennium?", "1000|100|10 000", "1000|100|10,000"),
            Q(6, "Ile dni ma zwykły rok?", "How many days are there in a normal year?", "365|366|360", "365|366|360"),
            Q(7, "Ile minut ma doba?", "How many minutes are there in a day and night (24 hours)?", "1440|1200|2400|720", "1440|1200|2400|720"),
            Q(7, "Ile sekund ma godzina?", "How many seconds are there in an hour?", "3600|600|6000|360", "3600|600|6000|360"),
            Q(7, "Ile miesięcy w roku ma 31 dni?", "How many months of the year have 31 days?", "7|6|5|8", "7|6|5|8"),
            Q(8, "Ile godzin ma tydzień?", "How many hours are there in a week?", "168|148|170|120", "168|148|170|120"),
            Q(8, "Ile miesięcy w roku ma 30 dni?", "How many months of the year have 30 days?", "4|5|6|3", "4|5|6|3"),
            Q(8, "Ile dni mają razem styczeń, luty i marzec w zwykłym roku?", "How many days do January, February and March have together in a normal year?", "90|91|92|89", "90|91|92|89"),
            Q(9, "Ile dni mają razem wszystkie miesiące, które mają po 30 dni?", "How many days do all the months with 30 days have together?", "120|150|90|180", "120|150|90|180"),
            Q(9, "Ile sekund ma kwadrans?", "How many seconds are there in a quarter of an hour?", "900|150|1500|600", "900|150|1500|600"),
            Q(9, "Ile dni ma rok przestępny w pełnych tygodniach i dniach?", "How many full weeks and extra days are in a leap year?", "52 tygodnie i 2 dni|52 tygodnie i 1 dzień|50 tygodni", "52 weeks and 2 days|52 weeks and 1 day|50 weeks"),
        };

        static readonly Item[] Nature =
        {
            Q(1, "Ile nóg ma pająk?", "How many legs does a spider have?", "8|6|4|10", "8|6|4|10"),
            Q(1, "Ile nóg ma owad?", "How many legs does an insect have?", "6|8|4", "6|8|4"),
            Q(1, "Z czego powstaje motyl?", "What does a butterfly grow from?", "z gąsienicy|z kijanki|ze ślimaka", "a caterpillar|a tadpole|a snail"),
            Q(1, "Jak nazywa się młoda żaba?", "What is a baby frog called?", "kijanka|źrebię|pisklę", "a tadpole|a foal|a chick"),
            Q(1, "Które zwierzę przesypia zimę?", "Which animal sleeps through the winter?", "niedźwiedź|wróbel|sarna", "bear|sparrow|roe deer"),
            Q(1, "Na którym drzewie rosną żołędzie?", "Which tree do acorns grow on?", "na dębie|na brzozie|na sośnie", "an oak|a birch|a pine"),
            Q(2, "Które drzewo ma igły?", "Which tree has needles?", "sosna|dąb|brzoza|klon", "pine|oak|birch|maple"),
            Q(2, "Który duży ptak przylatuje do Polski wiosną i buduje gniazda na dachach i słupach?", "Which big bird comes to Poland in spring and builds nests on roofs and poles?", "bocian biały|pingwin|struś", "the white stork|the penguin|the ostrich"),
            Q(2, "Czego potrzebują rośliny, żeby rosnąć?", "What do plants need to grow?", "wody i światła|cukru i soli|ciemności i zimna", "water and light|sugar and salt|darkness and cold"),
            QK(2, "Jakie ptaki pływają po jeziorze i kanale przy zamku w Kórniku?", "Which birds swim on the lake and canal by Kórnik Castle?", "łabędzie|pingwiny|strusie", "swans|penguins|ostriches"),
            QK(2, "Jak nazywa się słynny ogród pełen drzew i krzewów w Kórniku?", "What is the famous garden full of trees and shrubs in Kórnik called?", "Arboretum|Zoo|Akwarium", "the Arboretum|the Zoo|the Aquarium"),
            Q(2, "Który ptak stuka dziobem w drzewa, szukając owadów?", "Which bird knocks on trees with its beak looking for insects?", "dzięcioł|bocian|łabędź", "the woodpecker|the stork|the swan"),
            Q(2, "Który zmysł działa dzięki nosowi?", "Which sense works thanks to your nose?", "węch|wzrok|słuch", "smell|sight|hearing"),
            Q(3, "Które zwierzę jest ssakiem?", "Which animal is a mammal?", "wieloryb|rekin|żaba|pingwin", "whale|shark|frog|penguin"),
            Q(3, "Które zwierzę jest płazem?", "Which animal is an amphibian?", "żaba|jaszczurka|wąż", "frog|lizard|snake"),
            Q(3, "Który narząd pompuje krew w naszym ciele?", "Which organ pumps blood around our body?", "serce|płuca|żołądek", "the heart|the lungs|the stomach"),
            Q(3, "Którym narządem oddychamy?", "Which organs do we breathe with?", "płucami|sercem|żołądkiem", "the lungs|the heart|the stomach"),
            Q(3, "Które zwierzę jest roślinożercą?", "Which animal is a herbivore (eats only plants)?", "sarna|wilk|lis", "roe deer|wolf|fox"),
            Q(3, "Jak nazywa się dom bobrów zbudowany z gałęzi?", "What is a beavers' home made of branches called?", "żeremie|gniazdo|ul", "a lodge|a nest|a hive"),
            Q(3, "Która część rośliny pobiera wodę z ziemi?", "Which part of a plant takes water from the soil?", "korzeń|kwiat|liść", "the root|the flower|the leaf"),
            Q(3, "Co mają wszystkie ptaki?", "What do all birds have?", "pióra|zęby|futro", "feathers|teeth|fur"),
            Q(4, "Które zwierzę jest gadem?", "Which animal is a reptile?", "jaszczurka|żaba|traszka", "lizard|frog|newt"),
            Q(4, "Jak nazywa się największe dzikie zwierzę żyjące w Polsce, chronione m.in. w Puszczy Białowieskiej?", "What is the biggest wild animal living in Poland, protected for example in the Białowieża Forest?", "żubr|dzik|jeleń", "the European bison|the wild boar|the red deer"),
            Q(4, "Co jest między gąsienicą a dorosłym motylem?", "What comes between a caterpillar and an adult butterfly?", "poczwarka|kijanka|jajo", "a pupa (chrysalis)|a tadpole|an egg"),
            Q(4, "Jak nazywamy zwierzę, które poluje na inne zwierzęta?", "What do we call an animal that hunts other animals?", "drapieżnik|roślinożerca|owad", "a predator|a herbivore|an insect"),
            Q(4, "Jak nazywamy zwierzę, które je i rośliny, i mięso?", "What do we call an animal that eats both plants and meat?", "wszystkożerca|roślinożerca|mięsożerca", "an omnivore|a herbivore|a carnivore"),
            Q(4, "Ile skrzydeł ma pszczoła?", "How many wings does a bee have?", "4|2|6", "4|2|6"),
            Q(5, "Jaki gaz wydzielają rośliny w czasie fotosyntezy?", "Which gas do plants give off during photosynthesis?", "tlen|dwutlenek węgla|azot", "oxygen|carbon dioxide|nitrogen"),
            Q(5, "Co jest na początku każdego łańcucha pokarmowego?", "What is at the start of every food chain?", "rośliny|lisy|sowy", "plants|foxes|owls"),
            Q(5, "Łańcuch pokarmowy: trawa → zając → ... Kto zjada zająca?", "Food chain: grass → hare → ... Who eats the hare?", "lis|marchewka|motyl", "a fox|a carrot|a butterfly"),
            Q(5, "Który narząd steruje całym naszym ciałem?", "Which organ controls our whole body?", "mózg|żołądek|płuca", "the brain|the stomach|the lungs"),
            Q(5, "Co wyróżnia ssaki?", "What makes mammals special?", "karmią młode mlekiem|mają pióra|oddychają skrzelami", "they feed their babies milk|they have feathers|they breathe with gills"),
            Q(5, "Czym oddycha większość ryb?", "What do most fish breathe with?", "skrzelami|płucami|skórą", "gills|lungs|skin"),
            Q(5, "Które zwierzę lądowe jest najszybsze?", "Which land animal is the fastest?", "gepard|lew|koń", "the cheetah|the lion|the horse"),
            Q(5, "W którym narządzie trawi się pokarm po połknięciu?", "Which organ digests food after we swallow it?", "w żołądku|w płucach|w sercu", "the stomach|the lungs|the heart"),
            Q(6, "Jaki gaz pobierają rośliny z powietrza do fotosyntezy?", "Which gas do plants take from the air for photosynthesis?", "dwutlenek węgla|tlen|hel", "carbon dioxide|oxygen|helium"),
            Q(6, "Jak nazywa się zielony barwnik w liściach?", "What is the green pigment in leaves called?", "chlorofil|hemoglobina|melanina", "chlorophyll|haemoglobin|melanin"),
            Q(6, "Ile kości ma dorosły człowiek?", "How many bones does an adult human have?", "206|106|306|56", "206|106|306|56"),
            Q(6, "Który narząd oczyszcza krew i wytwarza mocz?", "Which organs clean the blood and make urine?", "nerki|płuca|serce", "the kidneys|the lungs|the heart"),
            Q(6, "Jak nazywa się przemiana kijanki w żabę albo gąsienicy w motyla?", "What is the change of a tadpole into a frog or a caterpillar into a butterfly called?", "przeobrażenie|fotosynteza|parowanie", "metamorphosis|photosynthesis|evaporation"),
            Q(6, "Które z tych morskich zwierząt jest ssakiem?", "Which of these sea animals is a mammal?", "delfin|rekin|ośmiornica", "the dolphin|the shark|the octopus"),
            Q(7, "Jaki jest największy narząd człowieka?", "What is the largest organ of the human body?", "skóra|wątroba|serce", "the skin|the liver|the heart"),
            Q(7, "Które zwierzę jest owadem?", "Which animal is an insect?", "biedronka|pająk|ślimak|kleszcz", "ladybird|spider|snail|tick"),
            Q(7, "Do jakiej grupy należą grzyby, np. borowik?", "Which group do mushrooms such as the cep belong to?", "do grzybów, osobnej grupy organizmów|do roślin|do zwierząt", "fungi, a group of their own|plants|animals"),
            Q(7, "Który z tych ptaków zostaje w Polsce na zimę?", "Which of these birds stays in Poland for the winter?", "wróbel|bocian|jaskółka", "the sparrow|the stork|the swallow"),
            Q(7, "Ile zębów mlecznych ma dziecko?", "How many milk teeth does a child have?", "20|32|10|28", "20|32|10|28"),
            Q(7, "Do czego służą białe krwinki?", "What do white blood cells do?", "bronią ciało przed chorobami|przenoszą tlen|trawią pokarm", "they fight diseases|they carry oxygen|they digest food"),
            Q(8, "Co przenosi tlen we krwi?", "What carries oxygen in the blood?", "czerwone krwinki|białe krwinki|płytki krwi", "red blood cells|white blood cells|platelets"),
            Q(8, "Jak nazywają się naczynia, które prowadzą krew od serca do całego ciała?", "What are the blood vessels that carry blood away from the heart called?", "tętnice|żyły|nerwy", "arteries|veins|nerves"),
            Q(8, "Jaka jest najdłuższa kość w ciele człowieka?", "What is the longest bone in the human body?", "kość udowa|kość ramienna|żebro", "the thigh bone (femur)|the upper arm bone|a rib"),
            Q(8, "Jak nazywa się w ekosystemie organizm, który sam wytwarza pokarm, np. roślina?", "In an ecosystem, what do we call a living thing that makes its own food, like a plant?", "producent|konsument|drapieżnik", "a producer|a consumer|a predator"),
            Q(8, "Która grupa zwierząt należy do bezkręgowców?", "Which group of animals are invertebrates (no backbone)?", "owady|ptaki|ryby", "insects|birds|fish"),
            Q(8, "Ile zębów stałych ma dorosły człowiek razem z zębami mądrości?", "How many adult teeth does a grown-up have, including wisdom teeth?", "32|20|28|36", "32|20|28|36"),
            Q(9, "Jak nazywa się proces, w którym rośliny wytwarzają pokarm z wody i dwutlenku węgla dzięki światłu?", "What is the process called in which plants make food from water and carbon dioxide using light?", "fotosynteza|oddychanie|parowanie", "photosynthesis|respiration|evaporation"),
            Q(9, "Gdzie w komórce roślinnej zachodzi fotosynteza?", "Where in a plant cell does photosynthesis take place?", "w chloroplastach|w jądrze komórkowym|w ścianie komórkowej", "in the chloroplasts|in the nucleus|in the cell wall"),
            Q(9, "Który narząd wytwarza żółć?", "Which organ makes bile?", "wątroba|trzustka|nerki", "the liver|the pancreas|the kidneys"),
            Q(9, "Który park narodowy słynie z żubrów?", "Which Polish national park is famous for its European bison?", "Białowieski|Tatrzański|Słowiński", "Białowieża National Park|Tatra National Park|Słowiński National Park"),
            Q(9, "Która grupa zwierząt jest zmiennocieplna?", "Which group of animals is cold-blooded?", "gady|ptaki|ssaki", "reptiles|birds|mammals"),
            Q(9, "Które drzewo rośnie najczęściej w polskich lasach?", "Which tree is the most common in Polish forests?", "sosna|dąb|buk", "the pine|the oak|the beech"),
        };

        static readonly Item[] Geography =
        {
            Q(1, "Jak nazywa się stolica Polski?", "What is the capital of Poland?", "Warszawa|Kraków|Gdańsk", "Warsaw|Kraków|Gdańsk"),
            Q(1, "Jakie kolory ma flaga Polski?", "What colours is the flag of Poland?", "biały i czerwony|niebieski i żółty|zielony i biały", "white and red|blue and yellow|green and white"),
            QK(1, "W jakim mieście jest nasza szkoła?", "In which town is our school?", "w Kórniku|w Gdańsku|w Krakowie", "Kórnik|Gdańsk|Kraków"),
            Q(1, "Jak nazywa się najdłuższa rzeka w Polsce?", "What is the longest river in Poland?", "Wisła|Odra|Warta", "the Vistula|the Oder|the Warta"),
            Q(1, "Nad jakim morzem leży Polska?", "Which sea is Poland next to?", "nad Bałtyckim|nad Czarnym|nad Czerwonym", "the Baltic Sea|the Black Sea|the Red Sea"),
            Q(1, "W którym mieście według legendy mieszkał Smok Wawelski?", "In which city did the Wawel Dragon live, according to the legend?", "w Krakowie|w Warszawie|w Poznaniu", "Kraków|Warsaw|Poznań"),
            QK(2, "Jaki piękny zabytek stoi w Kórniku nad jeziorem?", "Which beautiful old building stands by the lake in Kórnik?", "zamek|wieża Eiffla|piramida", "a castle|the Eiffel Tower|a pyramid"),
            Q(2, "Jaki ptak jest w godle Polski?", "Which bird is on the Polish emblem?", "biały orzeł|wróbel|bocian", "a white eagle|a sparrow|a stork"),
            Q(2, "Na którym kontynencie leży Polska?", "On which continent is Poland?", "w Europie|w Afryce|w Azji", "Europe|Africa|Asia"),
            QK(2, "Jak nazywa się duże miasto blisko Kórnika?", "Which big city is close to Kórnik?", "Poznań|Kraków|Gdańsk", "Poznań|Kraków|Gdańsk"),
            QK(2, "W jakim województwie leży Kórnik?", "In which province (voivodeship) is Kórnik?", "w wielkopolskim|w mazowieckim|w pomorskim", "Greater Poland (Wielkopolska)|Masovia|Pomerania"),
            Q(2, "Według legendy Lech założył gród tam, gdzie zobaczył gniazdo orła. Jak nazwał ten gród?", "According to the legend, Lech founded a town where he saw an eagle's nest. What did he call it?", "Gniezno|Kraków|Gdańsk", "Gniezno|Kraków|Gdańsk"),
            Q(2, "Gdzie wschodzi słońce?", "Where does the sun rise?", "na wschodzie|na zachodzie|na północy", "in the east|in the west|in the north"),
            Q(3, "Jak nazywają się najwyższe góry w Polsce?", "What are the highest mountains in Poland called?", "Tatry|Alpy|Andy", "the Tatras|the Alps|the Andes"),
            Q(3, "Ile jest kontynentów?", "How many continents are there?", "7|5|10", "7|5|10"),
            QK(3, "Nad jakim jeziorem leży Kórnik?", "Which lake is Kórnik next to?", "nad Jeziorem Kórnickim|nad Jeziorem Bodeńskim|nad Morzem Bałtyckim", "Lake Kórnik|Lake Constance|the Baltic Sea"),
            Q(3, "Na którym kontynencie żyją dziko kangury?", "On which continent do kangaroos live in the wild?", "w Australii|w Europie|w Ameryce Południowej", "Australia|Europe|South America"),
            QK(3, "Jak nazywa się legenda o pani z obrazu na zamku w Kórniku?", "What is the legend of the lady from the painting in Kórnik Castle called?", "Biała Dama|Smok Wawelski|Syrenka", "the White Lady|the Wawel Dragon|the Little Mermaid"),
            Q(3, "Który z tych krajów graniczy z Polską?", "Which of these countries borders Poland?", "Niemcy|Francja|Włochy|Hiszpania", "Germany|France|Italy|Spain"),
            Q(3, "Jak nazywa się hymn Polski?", "What is the Polish national anthem called?", "Mazurek Dąbrowskiego|Rota|Bogurodzica", "Mazurek Dąbrowskiego|Rota|Bogurodzica"),
            Q(3, "Jak nazywa się polska waluta?", "What is the Polish currency called?", "złoty|euro|funt", "the złoty|the euro|the pound"),
            Q(4, "Jak nazywa się największy ocean?", "What is the largest ocean?", "Spokojny|Atlantycki|Indyjski", "the Pacific|the Atlantic|the Indian"),
            QK(4, "Kto jest patronką naszej szkoły?", "Who is the patron of our school?", "Teofila z Działyńskich Szołdrska-Potulicka|Maria Konopnicka|Królowa Bona", "Teofila z Działyńskich Szołdrska-Potulicka|Maria Konopnicka|Queen Bona"),
            Q(4, "Kto był pierwszym historycznym władcą Polski?", "Who was the first historical ruler of Poland?", "Mieszko I|Bolesław Chrobry|Kazimierz Wielki", "Mieszko I|Bolesław the Brave|Casimir the Great"),
            Q(4, "W którym roku był chrzest Polski?", "In which year was the baptism of Poland?", "966|1410|1918", "966|1410|1918"),
            Q(4, "Które miasto jest stolicą Wielkopolski?", "Which city is the capital of Greater Poland (Wielkopolska)?", "Poznań|Gniezno|Kalisz", "Poznań|Gniezno|Kalisz"),
            Q(4, "Kiedy obchodzimy Narodowe Święto Niepodległości?", "When is Poland's National Independence Day?", "11 listopada|3 maja|1 stycznia", "11 November|3 May|1 January"),
            Q(4, "Jaka rzeka płynie przez Poznań?", "Which river flows through Poznań?", "Warta|Wisła|Odra", "the Warta|the Vistula|the Oder"),
            Q(5, "Które miasto było przez wiele lat stolicą Polski, zanim została nią Warszawa?", "Which city was the capital of Poland for many years before Warsaw?", "Kraków|Łódź|Szczecin", "Kraków|Łódź|Szczecin"),
            Q(5, "Jak nazywa się najwyższy szczyt w Polsce?", "What is the highest peak in Poland?", "Rysy|Giewont|Śnieżka", "Rysy|Giewont|Śnieżka"),
            QK(5, "Co według legendy robi Biała Dama z Kórnika o północy?", "According to the legend, what does the White Lady of Kórnik do at midnight?", "wychodzi z obrazu i spaceruje po zamku|gra w piłkę|łowi ryby w jeziorze", "steps out of her painting and walks around the castle|plays football|goes fishing in the lake"),
            Q(5, "Kto był pierwszym koronowanym królem Polski?", "Who was the first crowned king of Poland?", "Bolesław Chrobry|Mieszko I|Jan III Sobieski", "Bolesław the Brave|Mieszko I|John III Sobieski"),
            Q(5, "W którym roku była bitwa pod Grunwaldem?", "In which year was the Battle of Grunwald?", "1410|966|1791", "1410|966|1791"),
            Q(5, "Jak nazywa się zamek królewski w Krakowie?", "What is the royal castle in Kraków called?", "Wawel|Malbork|Wilanów", "Wawel|Malbork|Wilanów"),
            Q(5, "Jaka jest stolica Francji?", "What is the capital of France?", "Paryż|Berlin|Rzym", "Paris|Berlin|Rome"),
            Q(5, "Jaka jest stolica Niemiec?", "What is the capital of Germany?", "Berlin|Wiedeń|Praga", "Berlin|Vienna|Prague"),
            Q(5, "Który kontynent jest największy?", "Which continent is the largest?", "Azja|Europa|Australia", "Asia|Europe|Australia"),
            QK(6, "W którym roku urodziła się Teofila, patronka naszej szkoły?", "In which year was Teofila, our school's patron, born?", "1714|1914|2014", "1714|1914|2014"),
            QK(6, "Kto założył słynną Bibliotekę Kórnicką?", "Who founded the famous Kórnik Library?", "Tytus Działyński|Mikołaj Kopernik|Fryderyk Chopin", "Tytus Działyński|Nicolaus Copernicus|Frédéric Chopin"),
            QK(6, "Kto zbudował ratusz w Bninie w 1777 roku?", "Who had the town hall in Bnin built in 1777?", "Teofila Szołdrska-Potulicka|Tytus Działyński|Mieszko I", "Teofila Szołdrska-Potulicka|Tytus Działyński|Mieszko I"),
            Q(6, "Który król dowodził wojskami polskimi i litewskimi pod Grunwaldem?", "Which king led the Polish and Lithuanian armies at Grunwald?", "Władysław Jagiełło|Bolesław Chrobry|Jan III Sobieski", "Władysław Jagiełło|Bolesław the Brave|John III Sobieski"),
            Q(6, "Z iloma państwami graniczy Polska?", "How many countries does Poland border?", "7|5|9", "7|5|9"),
            Q(6, "Które państwo NIE graniczy z Polską?", "Which country does NOT border Poland?", "Austria|Czechy|Słowacja|Litwa", "Austria|Czechia|Slovakia|Lithuania"),
            Q(6, "Która rzeka tworzy dużą część granicy Polski z Niemcami?", "Which river forms a large part of Poland's border with Germany?", "Odra|Wisła|Warta", "the Oder|the Vistula|the Warta"),
            Q(6, "Jaka jest stolica Czech?", "What is the capital of Czechia?", "Praga|Bratysława|Wiedeń", "Prague|Bratislava|Vienna"),
            Q(6, "Jaki ocean leży między Europą a Ameryką?", "Which ocean lies between Europe and America?", "Atlantycki|Spokojny|Indyjski", "the Atlantic|the Pacific|the Indian"),
            Q(6, "Co upamiętnia święto 3 Maja?", "What does the 3rd of May holiday commemorate?", "uchwalenie Konstytucji 3 maja|chrzest Polski|bitwę pod Grunwaldem", "the Constitution of 3 May|the baptism of Poland|the Battle of Grunwald"),
            Q(7, "W którym mieście urodził się Mikołaj Kopernik?", "In which city was Nicolaus Copernicus born?", "w Toruniu|w Krakowie|w Gdańsku", "Toruń|Kraków|Gdańsk"),
            Q(7, "W którym roku uchwalono Konstytucję 3 maja?", "In which year was the Constitution of 3 May passed?", "1791|1918|1410", "1791|1918|1410"),
            Q(7, "W którym roku Polska odzyskała niepodległość?", "In which year did Poland regain its independence?", "1918|1791|1945", "1918|1791|1945"),
            Q(7, "Jak miała na imię czeska księżniczka, żona Mieszka I?", "What was the name of the Czech princess who married Mieszko I?", "Dobrawa|Jadwiga|Bona", "Dobrawa|Jadwiga|Bona"),
            Q(7, "Z kim walczyli Polacy i Litwini w bitwie pod Grunwaldem?", "Who did the Poles and Lithuanians fight at the Battle of Grunwald?", "z Krzyżakami|z Rzymianami|z Wikingami", "the Teutonic Knights|the Romans|the Vikings"),
            QK(7, "Kim według legendy jest Biała Dama z Kórnika?", "Who is the White Lady of Kórnik, according to the legend?", "Teofilą z Działyńskich|Marią Skłodowską-Curie|królową Boną", "Teofila z Działyńskich|Marie Skłodowska-Curie|Queen Bona"),
            Q(7, "Ile województw ma Polska?", "How many provinces (voivodeships) does Poland have?", "16|12|49", "16|12|49"),
            Q(7, "Które państwo jest największe na świecie?", "Which is the largest country in the world?", "Rosja|Chiny|Kanada", "Russia|China|Canada"),
            Q(7, "Jaka jest stolica Litwy?", "What is the capital of Lithuania?", "Wilno|Ryga|Tallinn", "Vilnius|Riga|Tallinn"),
            Q(7, "Jaka jest stolica Ukrainy?", "What is the capital of Ukraine?", "Kijów|Lwów|Mińsk", "Kyiv|Lviv|Minsk"),
            Q(8, "W którym mieście koronowano Bolesława Chrobrego w 1025 roku?", "In which city was Bolesław the Brave crowned in 1025?", "w Gnieźnie|w Warszawie|w Gdańsku", "Gniezno|Warsaw|Gdańsk"),
            Q(8, "O którym królu mówi się, że zastał Polskę drewnianą, a zostawił murowaną?", "Which king is said to have found Poland made of wood and left it made of stone?", "Kazimierz Wielki|Mieszko I|Jan III Sobieski", "Casimir the Great|Mieszko I|John III Sobieski"),
            Q(8, "Który król pokonał Turków pod Wiedniem w 1683 roku?", "Which king defeated the Ottoman army at Vienna in 1683?", "Jan III Sobieski|Bolesław Chrobry|Władysław Jagiełło", "John III Sobieski|Bolesław the Brave|Władysław Jagiełło"),
            QK(8, "Który ród przebudował zamek w Kórniku w XIX wieku?", "Which family rebuilt Kórnik Castle in the 19th century?", "Działyńscy|Jagiellonowie|Piastowie", "the Działyński family|the Jagiellons|the Piasts"),
            QK(8, "W którym powiecie leży Kórnik?", "In which county (powiat) is Kórnik?", "w poznańskim|w gnieźnieńskim|w krakowskim", "Poznań County|Gniezno County|Kraków County"),
            Q(8, "Jak nazywa się linia, która dzieli Ziemię na półkulę północną i południową?", "What is the line that divides the Earth into the northern and southern hemispheres called?", "równik|południk zerowy|zwrotnik", "the equator|the prime meridian|the tropic"),
            Q(8, "Jaka jest stolica Słowacji?", "What is the capital of Slovakia?", "Bratysława|Budapeszt|Praga", "Bratislava|Budapest|Prague"),
            Q(9, "W którym roku Polska wstąpiła do Unii Europejskiej?", "In which year did Poland join the European Union?", "2004|1989|1999", "2004|1989|1999"),
            Q(9, "Który król założył w 1364 roku Akademię Krakowską?", "Which king founded the Kraków Academy in 1364?", "Kazimierz Wielki|Mieszko I|Bolesław Chrobry", "Casimir the Great|Mieszko I|Bolesław the Brave"),
            QK(9, "W którym wieku Tytus Działyński założył Bibliotekę Kórnicką?", "In which century did Tytus Działyński found the Kórnik Library?", "w XIX|w XV|w XXI", "the 19th|the 15th|the 21st"),
            QK(9, "W którym wieku zbudowano ratusz w Bninie?", "In which century was the town hall in Bnin built?", "w XVIII|w XVI|w XX", "the 18th|the 16th|the 20th"),
            QK(9, "Kto przekazał zamek w Kórniku i jego zbiory narodowi polskiemu?", "Who gave Kórnik Castle and its collections to the Polish nation?", "Władysław Zamoyski|Tytus Działyński|Jan Matejko", "Władysław Zamoyski|Tytus Działyński|Jan Matejko"),
            Q(9, "Z jakim państwem Polska połączyła się w unii lubelskiej w 1569 roku?", "Which state did Poland join with in the Union of Lublin in 1569?", "z Litwą|z Czechami|z Węgrami", "Lithuania|Bohemia|Hungary"),
            Q(9, "Jak nazywa się dzieło Mikołaja Kopernika o ruchu planet?", "What is the name of Copernicus's book about how the planets move?", "O obrotach sfer niebieskich|Pan Tadeusz|Quo vadis", "On the Revolutions of the Heavenly Spheres|Pan Tadeusz|Quo Vadis"),
        };

        static readonly Item[] MusicArt =
        {
            Q(1, "Jaki kolor powstanie z żółtego i niebieskiego?", "What colour do you get from yellow and blue?", "zielony|czerwony|biały", "green|red|white"),
            Q(1, "Jaki kolor powstanie z czerwonego i żółtego?", "What colour do you get from red and yellow?", "pomarańczowy|fioletowy|zielony", "orange|purple|green"),
            Q(1, "Który instrument ma struny?", "Which instrument has strings?", "gitara|trąbka|bęben", "guitar|trumpet|drum"),
            Q(1, "Kto kieruje grą orkiestry?", "Who leads an orchestra?", "dyrygent|kompozytor|widz", "the conductor|the composer|the audience"),
            Q(2, "Jaki kolor powstanie z czerwonego i niebieskiego?", "What colour do you get from red and blue?", "fioletowy|zielony|pomarańczowy", "purple|green|orange"),
            Q(2, "Ile linii ma pięciolinia?", "How many lines does a music staff have?", "5|3|7", "5|3|7"),
            QK(2, "W jakiej sukni namalowano Białą Damę z Kórnika?", "What colour dress is the White Lady of Kórnik painted in?", "w białej|w czarnej|w zielonej", "white|black|green"),
            Q(2, "Które kolory są podstawowe?", "Which are the primary colours?", "czerwony, żółty, niebieski|zielony, różowy, szary|czarny, biały, brązowy", "red, yellow, blue|green, pink, grey|black, white, brown"),
            Q(2, "Do jakiej grupy instrumentów należy bęben?", "Which group of instruments does the drum belong to?", "perkusyjnych|strunowych|dętych", "percussion|strings|wind"),
            Q(3, "Który polski kompozytor pisał piękną muzykę na fortepian?", "Which Polish composer wrote beautiful piano music?", "Fryderyk Chopin|Mikołaj Kopernik|Adam Mickiewicz", "Frédéric Chopin|Nicolaus Copernicus|Adam Mickiewicz"),
            Q(3, "Do jakiej grupy instrumentów należy flet?", "Which group of instruments does the flute belong to?", "dętych|strunowych|perkusyjnych", "wind|strings|percussion"),
            Q(3, "Do jakiej grupy instrumentów należą skrzypce?", "Which group of instruments does the violin belong to?", "strunowych|dętych|perkusyjnych", "strings|wind|percussion"),
            Q(3, "Który kolor jest ciepły?", "Which colour is a warm colour?", "pomarańczowy|niebieski|błękitny", "orange|blue|light blue"),
            Q(3, "Jaki dźwięk jest po „mi” w gamie do, re, mi...?", "In do, re, mi..., which note comes after mi?", "fa|sol|re", "fa|sol|re"),
            Q(3, "Jak nazywa się obraz przedstawiający twarz lub postać człowieka?", "What is a painting of a person called?", "portret|pejzaż|martwa natura", "a portrait|a landscape|a still life"),
            Q(4, "Kto namalował „Mona Lizę”?", "Who painted the Mona Lisa?", "Leonardo da Vinci|Jan Matejko|Pablo Picasso", "Leonardo da Vinci|Jan Matejko|Pablo Picasso"),
            Q(4, "Do jakiej grupy instrumentów należy trąbka?", "Which group of instruments does the trumpet belong to?", "dętych blaszanych|dętych drewnianych|strunowych", "brass|woodwind|strings"),
            Q(4, "Jak nazywa się znak na początku pięciolinii, od którego zaczyna się zapis melodii?", "What is the sign at the start of the staff called?", "klucz wiolinowy|pauza|kreska taktowa", "the treble clef|a rest|a bar line"),
            Q(4, "Który kolor jest zimny?", "Which colour is a cold colour?", "niebieski|czerwony|pomarańczowy|żółty", "blue|red|orange|yellow"),
            Q(4, "Który kolor jest pochodny (powstaje z dwóch podstawowych)?", "Which colour is a secondary colour (made from two primary ones)?", "zielony|czerwony|żółty|niebieski", "green|red|yellow|blue"),
            Q(4, "Ilu muzyków gra w duecie?", "How many musicians play in a duet?", "2|3|4", "2|3|4"),
            Q(5, "Kto namalował obraz „Bitwa pod Grunwaldem”?", "Who painted the \"Battle of Grunwald\"?", "Jan Matejko|Leonardo da Vinci|Vincent van Gogh", "Jan Matejko|Leonardo da Vinci|Vincent van Gogh"),
            Q(5, "Co oznacza w muzyce słowo „forte”?", "What does \"forte\" mean in music?", "głośno|cicho|wolno", "loudly|quietly|slowly"),
            Q(5, "Co oznacza w muzyce słowo „piano”?", "What does \"piano\" mean as a music direction?", "cicho|głośno|szybko", "quietly|loudly|quickly"),
            Q(5, "Jakie polskie tańce często komponował Fryderyk Chopin?", "Which Polish dances did Frédéric Chopin often compose?", "mazurki i polonezy|tanga i samby|rock and rolla i disco", "mazurkas and polonaises|tangos and sambas|rock and roll and disco"),
            Q(5, "Akwarele to farby, które rozpuszcza się w...", "Watercolours are paints that you mix with...", "wodzie|oleju|mleku", "water|oil|milk"),
            Q(5, "Jak nazywa się obraz przedstawiający krajobraz?", "What is a painting of the countryside or scenery called?", "pejzaż|portret|autoportret", "a landscape|a portrait|a self-portrait"),
            Q(6, "Kto namalował słynne „Słoneczniki”?", "Who painted the famous \"Sunflowers\"?", "Vincent van Gogh|Jan Matejko|Leonardo da Vinci", "Vincent van Gogh|Jan Matejko|Leonardo da Vinci"),
            Q(6, "Który kompozytor jest nazywany ojcem polskiej opery narodowej?", "Which composer is called the father of Polish national opera?", "Stanisław Moniuszko|Fryderyk Chopin|Wolfgang Amadeusz Mozart", "Stanisław Moniuszko|Frédéric Chopin|Wolfgang Amadeus Mozart"),
            Q(6, "Który kompozytor z Salzburga już jako małe dziecko koncertował w całej Europie?", "Which composer from Salzburg gave concerts all over Europe as a small child?", "Wolfgang Amadeusz Mozart|Fryderyk Chopin|Stanisław Moniuszko", "Wolfgang Amadeus Mozart|Frédéric Chopin|Stanisław Moniuszko"),
            Q(6, "Co oznacza w muzyce słowo „allegro”?", "What does \"allegro\" mean in music?", "szybko, żywo|wolno|cicho", "fast and lively|slowly|quietly"),
            Q(6, "Ile półnut trwa tyle, co jedna cała nuta?", "How many half notes last as long as one whole note?", "2|4|3", "2|4|3"),
            Q(6, "Jak nazywa się obraz przedstawiający np. owoce, kwiaty i naczynia?", "What is a painting of things like fruit, flowers and jugs called?", "martwa natura|pejzaż|portret", "a still life|a landscape|a portrait"),
            Q(7, "Gdzie urodził się Fryderyk Chopin?", "Where was Frédéric Chopin born?", "w Żelazowej Woli|w Krakowie|w Kórniku", "Żelazowa Wola|Kraków|Kórnik"),
            Q(7, "Ile strun mają skrzypce?", "How many strings does a violin have?", "4|6|3", "4|6|3"),
            Q(7, "Ile strun ma zwykła gitara klasyczna?", "How many strings does a standard classical guitar have?", "6|4|12", "6|4|12"),
            Q(7, "Ile ćwierćnut trwa tyle, co jedna cała nuta?", "How many quarter notes last as long as one whole note?", "4|2|8", "4|2|8"),
            Q(7, "Co oznacza w muzyce słowo „lento”?", "What does \"lento\" mean in music?", "wolno|szybko|głośno", "slowly|fast|loudly"),
            Q(7, "W jakim mieście żył i tworzył Jan Matejko?", "In which city did Jan Matejko live and paint?", "w Krakowie|w Poznaniu|w Gdańsku", "Kraków|Poznań|Gdańsk"),
            Q(8, "Kto skomponował operę „Halka”?", "Who composed the opera \"Halka\"?", "Stanisław Moniuszko|Fryderyk Chopin|Wolfgang Amadeusz Mozart", "Stanisław Moniuszko|Frédéric Chopin|Wolfgang Amadeus Mozart"),
            Q(8, "Kto skomponował operę „Czarodziejski flet”?", "Who composed the opera \"The Magic Flute\"?", "Wolfgang Amadeusz Mozart|Stanisław Moniuszko|Fryderyk Chopin", "Wolfgang Amadeus Mozart|Stanisław Moniuszko|Frédéric Chopin"),
            Q(8, "W którym muzeum można zobaczyć „Mona Lizę”?", "In which museum can you see the Mona Lisa?", "w Luwrze w Paryżu|w Wawelu w Krakowie|w British Museum w Londynie", "the Louvre in Paris|Wawel in Kraków|the British Museum in London"),
            Q(8, "Ilu muzyków gra w kwartecie?", "How many musicians play in a quartet?", "4|3|5", "4|3|5"),
            Q(8, "Jak nazywa się znak, który podwyższa dźwięk o pół tonu?", "What is the sign that raises a note by a semitone called?", "krzyżyk|bemol|kasownik", "a sharp|a flat|a natural"),
            Q(8, "Ile ćwierćnut trwa półnuta?", "How many quarter notes does a half note last?", "2|4|3", "2|4|3"),
            Q(9, "Co oznacza w muzyce słowo „crescendo”?", "What does \"crescendo\" mean in music?", "coraz głośniej|coraz ciszej|bardzo wolno", "getting louder|getting quieter|very slowly"),
            Q(9, "Ile ćwierćnut trwa półnuta z kropką?", "How many quarter notes does a dotted half note last?", "3|2|4", "3|2|4"),
            Q(9, "Na jakim instrumencie gra się hejnał z wieży Kościoła Mariackiego w Krakowie?", "Which instrument is the bugle call from St Mary's Church tower in Kraków played on?", "na trąbce|na skrzypcach|na bębnie", "the trumpet|the violin|the drum"),
            Q(9, "Co ile lat odbywa się w Warszawie Międzynarodowy Konkurs Pianistyczny im. Fryderyka Chopina?", "How often is the International Chopin Piano Competition held in Warsaw?", "co 5 lat|co roku|co 10 lat", "every 5 years|every year|every 10 years"),
            Q(9, "Kto namalował „Gwiaździstą noc”?", "Who painted \"The Starry Night\"?", "Vincent van Gogh|Jan Matejko|Leonardo da Vinci", "Vincent van Gogh|Jan Matejko|Leonardo da Vinci"),
            Q(9, "Który słynny kompozytor stracił słuch, a mimo to dalej komponował?", "Which famous composer lost his hearing but kept on composing?", "Ludwig van Beethoven|Fryderyk Chopin|Stanisław Moniuszko", "Ludwig van Beethoven|Frédéric Chopin|Stanisław Moniuszko"),
            Q(9, "Jak nazywa się znak, który obniża dźwięk o pół tonu?", "What is the sign that lowers a note by a semitone called?", "bemol|krzyżyk|kasownik", "a flat|a sharp|a natural"),
        };

        static readonly Item[] Health =
        {
            Q(1, "Ile razy dziennie myjemy zęby?", "How many times a day should we brush our teeth?", "co najmniej 2 razy|raz w tygodniu|nigdy", "at least twice|once a week|never"),
            Q(1, "Co robimy przed jedzeniem?", "What do we do before eating?", "myjemy ręce|idziemy spać|biegamy", "wash our hands|go to sleep|go running"),
            Q(1, "Na jakim świetle przechodzimy przez ulicę?", "On which light do we cross the street?", "na zielonym|na czerwonym|na żółtym", "green|red|yellow"),
            Q(1, "Co jest najzdrowsze na drugie śniadanie?", "Which is the healthiest school snack?", "kanapka z warzywami|chipsy|batonik", "a sandwich with vegetables|crisps|a chocolate bar"),
            Q(2, "Co zakładamy na głowę, jadąc na rowerze?", "What do we wear on our head when riding a bike?", "kask|koronę|kapelusz", "a helmet|a crown|a hat"),
            Q(2, "Pod jaki numer dzwonimy w nagłym wypadku?", "Which number do we call in an emergency?", "112|500|321", "112|500|321"),
            Q(2, "Ile godzin snu potrzebuje w nocy dziecko w twoim wieku?", "How many hours of sleep does a child of your age need at night?", "około 9–11|około 2–3|około 15–16", "about 9–11|about 2–3|about 15–16"),
            Q(2, "Który napój jest najzdrowszy na co dzień?", "Which drink is the healthiest for every day?", "woda|cola|napój energetyczny", "water|cola|an energy drink"),
            Q(2, "W jakim sporcie używa się rakiety i lotki?", "Which sport uses a racket and a shuttlecock?", "badminton|koszykówka|pływanie", "badminton|basketball|swimming"),
            Q(3, "Ilu zawodników jednej drużyny gra na boisku w piłce nożnej?", "How many players from one team are on the pitch in football?", "11|6|15", "11|6|15"),
            Q(3, "Co ma dużo wapnia, dobrego dla kości i zębów?", "What has lots of calcium for strong bones and teeth?", "mleko|cukier|ketchup", "milk|sugar|ketchup"),
            Q(3, "Co musi świecić na rowerze po zmroku?", "What must be switched on on a bike after dark?", "światła z przodu i z tyłu|radio|nic", "lights at the front and back|a radio|nothing"),
            Q(3, "Czego powinniśmy jeść najwięcej każdego dnia?", "What should we eat most of every day?", "warzyw i owoców|słodyczy|chipsów", "vegetables and fruit|sweets|crisps"),
            Q(3, "Jak często warto się ruszać i bawić na świeżym powietrzu?", "How often should children be active and play outside?", "codziennie|raz w miesiącu|nigdy", "every day|once a month|never"),
            Q(3, "Co robimy, kiedy kichamy?", "What should we do when we sneeze?", "zasłaniamy usta i nos chusteczką lub łokciem|kichamy na kolegę|nic", "cover our mouth and nose with a tissue or elbow|sneeze at a friend|nothing"),
            Q(4, "Ile kół jest w symbolu olimpijskim?", "How many rings are in the Olympic symbol?", "5|3|7", "5|3|7"),
            Q(4, "Co ile lat odbywają się letnie igrzyska olimpijskie?", "How often are the Summer Olympic Games held?", "co 4 lata|co 2 lata|co roku", "every 4 years|every 2 years|every year"),
            Q(4, "Ilu zawodników jednej drużyny gra na boisku w siatkówce?", "How many players from one team are on the court in volleyball?", "6|11|5", "6|11|5"),
            Q(4, "Ilu zawodników jednej drużyny gra na boisku w koszykówce?", "How many players from one team are on the court in basketball?", "5|6|11", "5|6|11"),
            Q(4, "Którą stroną drogi idzie pieszy, gdy nie ma chodnika?", "In Poland, which side of the road do you walk on when there is no pavement?", "lewą|prawą|środkiem", "the left side|the right side|the middle"),
            Q(4, "Od ilu lat można w Polsce zdobyć kartę rowerową?", "From what age can you get a cycling licence (karta rowerowa) in Poland?", "od 10 lat|od 6 lat|od 16 lat", "10|6|16"),
            Q(5, "Co robimy, gdy kolega oparzy się gorącą wodą?", "What should you do when a friend is burned by hot water?", "chłodzimy oparzone miejsce wodą|smarujemy je masłem|posypujemy je solą", "cool the burn with water|put butter on it|put salt on it"),
            Q(5, "Które produkty są dobrym źródłem białka?", "Which foods are a good source of protein?", "jajka i ryby|cukierki i lizaki|chipsy i cola", "eggs and fish|sweets and lollipops|crisps and cola"),
            Q(5, "W jakim sporcie liczy się gemy i sety?", "In which sport do you count games and sets?", "w tenisie|w piłce nożnej|w pływaniu", "tennis|football|swimming"),
            Q(5, "W jakiej dyscyplinie sportu słynie Robert Lewandowski?", "Which sport is Robert Lewandowski famous for?", "w piłce nożnej|w skokach narciarskich|w tenisie", "football|ski jumping|tennis"),
            Q(5, "W jakiej dyscyplinie sportu słynie Iga Świątek?", "Which sport is Iga Świątek famous for?", "w tenisie|w pływaniu|w siatkówce", "tennis|swimming|volleyball"),
            Q(5, "Ile posiłków dziennie zaleca się dzieciom?", "How many meals a day are recommended for children?", "4–5|1|10", "4–5|1|10"),
            Q(6, "Ile kilometrów ma maraton?", "How long is a marathon?", "około 42 km|około 10 km|około 100 km", "about 42 km|about 10 km|about 100 km"),
            Q(6, "W jakim kraju odbywały się igrzyska olimpijskie w starożytności?", "In which country were the ancient Olympic Games held?", "w Grecji|we Włoszech|w Egipcie", "Greece|Italy|Egypt"),
            Q(6, "Co robimy, gdy leci krew z nosa?", "What should you do when you have a nosebleed?", "pochylamy głowę lekko do przodu|odchylamy głowę mocno do tyłu|kładziemy się na plecach", "lean your head slightly forward|tip your head far back|lie down on your back"),
            Q(6, "Jaka witamina powstaje w skórze dzięki słońcu?", "Which vitamin does our skin make in sunlight?", "witamina D|witamina C|witamina A", "vitamin D|vitamin C|vitamin A"),
            Q(6, "Ilu zawodników jednej drużyny gra na boisku w piłce ręcznej?", "How many players from one team are on the court in handball?", "7|5|11", "7|5|11"),
            Q(6, "W jakiej dyscyplinie sportu słynął Adam Małysz?", "Which sport was Adam Małysz famous for?", "w skokach narciarskich|w piłce nożnej|w boksie", "ski jumping|football|boxing"),
            Q(7, "Ile minut trwa mecz piłki nożnej bez dogrywki?", "How many minutes does a football match last without extra time?", "90|60|120", "90|60|120"),
            Q(7, "Co trzeba zrobić przede wszystkim, zanim podejdziesz do poszkodowanego?", "What must you do first, before going to help an injured person?", "sprawdzić, czy jest bezpiecznie|zrobić zdjęcie|zacząć biec", "check that it is safe|take a photo|start running"),
            Q(7, "Który chleb ma najwięcej błonnika?", "Which bread has the most fibre?", "pełnoziarnisty|biała bułka|drożdżówka", "wholemeal bread|a white roll|a sweet bun"),
            Q(7, "Co pokazuje piramida zdrowego żywienia?", "What does the healthy eating pyramid show?", "co i ile jeść, żeby być zdrowym|jak zbudować piramidę|kto wygrał zawody", "what and how much to eat to stay healthy|how to build a pyramid|who won a race"),
            Q(7, "Ile setów trzeba wygrać, żeby wygrać mecz siatkówki?", "How many sets must a team win to win a volleyball match?", "3|2|5", "3|2|5"),
            Q(7, "Ile punktów daje w koszykówce celny rzut zza linii za trzy punkty?", "How many points is a basket from behind the three-point line worth?", "3|2|1", "3|2|1"),
            Q(8, "W jakiej pozycji układamy osobę nieprzytomną, która oddycha?", "How should you place an unconscious person who is breathing?", "w pozycji bocznej bezpiecznej|na brzuchu z rękami w górze|na siedząco", "in the recovery position|on their tummy with arms up|sitting up"),
            Q(8, "Ile uciśnięć klatki piersiowej przypada na 2 oddechy ratownicze przy resuscytacji?", "During CPR, how many chest compressions are given for every 2 rescue breaths?", "30|10|50", "30|10|50"),
            Q(8, "Co mówią nam kalorie podane na jedzeniu?", "What do the calories on food labels tell us?", "ile energii daje jedzenie|ile w nim wody|jakiego jest koloru", "how much energy the food gives|how much water it has|what colour it is"),
            Q(8, "Gdzie najłatwiej zmierzyć sobie tętno?", "Where is the easiest place to feel your pulse?", "na nadgarstku lub szyi|na kolanie|na łokciu", "on your wrist or neck|on your knee|on your elbow"),
            Q(8, "Ile trwa jedna połowa meczu piłki nożnej?", "How long is one half of a football match?", "45 minut|30 minut|60 minut", "45 minutes|30 minutes|60 minutes"),
            Q(8, "Ile kółek ma flaga olimpijska w każdym rzędzie (górnym i dolnym)?", "How many rings are in the top row and in the bottom row of the Olympic symbol?", "3 i 2|2 i 2|4 i 1", "3 and 2|2 and 2|4 and 1"),
            Q(9, "Ile dokładnie metrów ma maraton?", "Exactly how many metres is a marathon?", "42 195|40 000|50 000", "42,195|40,000|50,000"),
            Q(9, "Ilu zawodników jednej drużyny gra w siatkówce plażowej?", "How many players are on one team in beach volleyball?", "2|4|6", "2|4|6"),
            Q(9, "Które z tych miast było gospodarzem meczów Euro 2012?", "Which of these cities hosted matches at Euro 2012?", "Poznań|Kraków|Łódź", "Poznań|Kraków|Łódź"),
            Q(9, "W jakiej dyscyplinie Robert Korzeniowski zdobył 4 złote medale olimpijskie?", "In which sport did Robert Korzeniowski win 4 Olympic gold medals?", "w chodzie sportowym|w pływaniu|w boksie", "race walking|swimming|boxing"),
            Q(9, "Do czego służy AED (defibrylator)?", "What is an AED (defibrillator) used for?", "do przywracania prawidłowej pracy serca|do mierzenia temperatury|do pompowania piłek", "to help a stopped heart beat normally again|to take your temperature|to pump up balls"),
            Q(9, "Ile lat mija między letnimi igrzyskami olimpijskimi a najbliższymi zimowymi?", "How many years are there between a Summer Olympics and the next Winter Olympics?", "2|4|1", "2|4|1"),
        };

        static readonly Item[] Science =
        {
            Q(1, "Słońce to...", "The Sun is a...", "gwiazda|planeta|księżyc", "star|planet|moon"),
            Q(1, "Co przyciąga magnes?", "What does a magnet pull?", "żelazne gwoździe|drewno|papier", "iron nails|wood|paper"),
            Q(1, "W co zmienia się woda w zamrażarce?", "What does water turn into in a freezer?", "w lód|w parę|w piasek", "ice|steam|sand"),
            Q(1, "Na jakiej planecie mieszkamy?", "Which planet do we live on?", "na Ziemi|na Marsie|na Jowiszu", "Earth|Mars|Jupiter"),
            Q(2, "Co krąży wokół Ziemi?", "What goes around the Earth?", "Księżyc|Słońce|Mars", "the Moon|the Sun|Mars"),
            Q(2, "W jakiej temperaturze zamarza woda?", "At what temperature does water freeze?", "0 °C|10 °C|100 °C", "0 °C|10 °C|100 °C"),
            Q(2, "Ile planet krąży wokół Słońca?", "How many planets go around the Sun?", "8|3|12", "8|3|12"),
            Q(2, "Jaki stan skupienia ma lód?", "What state of matter is ice?", "stały|ciekły|gazowy", "solid|liquid|gas"),
            Q(2, "Co się dzieje z wodą, kiedy się gotuje?", "What happens to water when it boils?", "zamienia się w parę|zamienia się w lód|robi się słodka", "it turns into steam|it turns into ice|it becomes sweet"),
            Q(3, "Która planeta jest nazywana Czerwoną Planetą?", "Which planet is called the Red Planet?", "Mars|Wenus|Saturn", "Mars|Venus|Saturn"),
            Q(3, "Która planeta jest największa?", "Which planet is the biggest?", "Jowisz|Merkury|Ziemia", "Jupiter|Mercury|Earth"),
            Q(3, "Która planeta jest najbliżej Słońca?", "Which planet is closest to the Sun?", "Merkury|Ziemia|Neptun", "Mercury|Earth|Neptune"),
            Q(3, "Która planeta słynie z pięknych pierścieni?", "Which planet is famous for its beautiful rings?", "Saturn|Merkury|Mars", "Saturn|Mercury|Mars"),
            Q(3, "Jak nazywają się dwa końce magnesu?", "What are the two ends of a magnet called?", "bieguny|rogi|szczyty", "poles|horns|peaks"),
            Q(3, "W jakiej temperaturze wrze woda (na poziomie morza)?", "At what temperature does water boil (at sea level)?", "100 °C|50 °C|200 °C", "100 °C|50 °C|200 °C"),
            Q(4, "Kto odkrył, że Ziemia krąży wokół Słońca?", "Who discovered that the Earth goes around the Sun?", "Mikołaj Kopernik|Fryderyk Chopin|Maria Konopnicka", "Nicolaus Copernicus|Frédéric Chopin|Christopher Columbus"),
            Q(4, "Ile czasu Ziemia okrąża Słońce?", "How long does the Earth take to go around the Sun?", "rok|dzień|tydzień", "one year|one day|one week"),
            Q(4, "Dlaczego mamy dzień i noc?", "Why do we have day and night?", "bo Ziemia obraca się wokół własnej osi|bo Słońce w nocy gaśnie|bo Księżyc zasłania Słońce", "because the Earth spins around|because the Sun switches off at night|because the Moon covers the Sun"),
            Q(4, "Co dobrze przewodzi prąd?", "What conducts electricity well?", "metalowy drut|gumowa rękawica|drewniany patyk", "a metal wire|a rubber glove|a wooden stick"),
            Q(4, "Które bieguny dwóch magnesów się przyciągają?", "Which poles of two magnets attract each other?", "różne (N i S)|jednakowe (N i N)|żadne", "opposite ones (N and S)|the same ones (N and N)|none"),
            Q(4, "Czym mierzymy temperaturę?", "What do we measure temperature with?", "termometrem|linijką|wagą", "a thermometer|a ruler|scales"),
            Q(5, "Która planeta jest trzecia od Słońca?", "Which planet is third from the Sun?", "Ziemia|Mars|Wenus", "Earth|Mars|Venus"),
            Q(5, "Co jest szybsze: światło czy dźwięk?", "Which is faster: light or sound?", "światło|dźwięk|są tak samo szybkie", "light|sound|they are equally fast"),
            Q(5, "Jak nazywa się siła, która przyciąga nas do Ziemi?", "What is the force that pulls us towards the Earth called?", "grawitacja|magnetyzm|tarcie", "gravity|magnetism|friction"),
            Q(5, "Jaki stan skupienia ma woda płynąca z kranu?", "What state of matter is water from the tap?", "ciekły|stały|gazowy", "liquid|solid|gas"),
            Q(5, "Jaki stan skupienia ma powietrze?", "What state of matter is air?", "gazowy|stały|ciekły", "gas|solid|liquid"),
            Q(5, "Co pokazuje kompas?", "What does a compass show?", "kierunki świata|temperaturę|godzinę", "the directions (north, south...)|the temperature|the time"),
            Q(6, "Która planeta jest najdalej od Słońca?", "Which planet is farthest from the Sun?", "Neptun|Uran|Saturn|Mars", "Neptune|Uranus|Saturn|Mars"),
            Q(6, "Dlaczego w kosmosie panuje cisza?", "Why is space silent?", "bo nie ma tam powietrza, które niesie dźwięk|bo jest tam zimno|bo jest tam ciemno", "because there is no air to carry sound|because it is cold|because it is dark"),
            Q(6, "Dlaczego najpierw widzimy błyskawicę, a dopiero potem słyszymy grzmot?", "Why do we see lightning before we hear the thunder?", "bo światło jest szybsze od dźwięku|bo uszy są wolniejsze od oczu|bo grzmot jest później", "because light is faster than sound|because ears are slower than eyes|because the thunder happens later"),
            Q(6, "Kto pierwszy stanął na Księżycu?", "Who was the first person to walk on the Moon?", "Neil Armstrong|Jurij Gagarin|Mirosław Hermaszewski", "Neil Armstrong|Yuri Gagarin|Mirosław Hermaszewski"),
            Q(6, "Jak nazywa się zamiana lodu w wodę?", "What is it called when ice turns into water?", "topnienie|parowanie|krzepnięcie", "melting|evaporation|freezing"),
            Q(6, "Ile czasu Ziemia obraca się wokół własnej osi?", "How long does the Earth take to spin around once?", "około 24 godzin|około roku|około tygodnia", "about 24 hours|about a year|about a week"),
            Q(7, "Kto był pierwszym człowiekiem w kosmosie?", "Who was the first person in space?", "Jurij Gagarin|Neil Armstrong|Mikołaj Kopernik", "Yuri Gagarin|Neil Armstrong|Nicolaus Copernicus"),
            Q(7, "Która polska uczona dwa razy dostała Nagrodę Nobla?", "Which Polish scientist won the Nobel Prize twice?", "Maria Skłodowska-Curie|Maria Konopnicka|Irena Sendlerowa", "Marie Skłodowska-Curie|Maria Konopnicka|Irena Sendler"),
            Q(7, "Co według Kopernika jest w środku naszego układu planet?", "According to Copernicus, what is at the centre of our planetary system?", "Słońce|Ziemia|Księżyc", "the Sun|the Earth|the Moon"),
            Q(7, "Który materiał jest izolatorem, czyli nie przewodzi prądu?", "Which material is an insulator (does not conduct electricity)?", "plastik|miedź|żelazo|aluminium", "plastic|copper|iron|aluminium"),
            Q(7, "Ile czasu Księżyc okrąża Ziemię?", "How long does the Moon take to go around the Earth?", "około miesiąca|jeden dzień|rok", "about a month|one day|one year"),
            Q(7, "Jak nazywa się zamiana wody w lód?", "What is it called when water turns into ice?", "krzepnięcie (zamarzanie)|topnienie|parowanie", "freezing|melting|evaporation"),
            Q(8, "Jaki pierwiastek Maria Skłodowska-Curie nazwała na cześć Polski?", "Which element did Marie Skłodowska-Curie name after Poland?", "polon|rad|uran", "polonium|radium|uranium"),
            Q(8, "Kto był pierwszym Polakiem w kosmosie?", "Who was the first Pole in space?", "Mirosław Hermaszewski|Neil Armstrong|Jurij Gagarin", "Mirosław Hermaszewski|Neil Armstrong|Yuri Gagarin"),
            Q(8, "Jak nazywa się nasza galaktyka?", "What is our galaxy called?", "Droga Mleczna|Andromeda|Wielki Wóz", "the Milky Way|Andromeda|the Plough"),
            Q(8, "Kiedy żarówka w obwodzie elektrycznym świeci?", "When does a bulb in an electric circuit light up?", "gdy obwód jest zamknięty|gdy obwód jest otwarty|gdy nie ma baterii", "when the circuit is closed|when the circuit is open|when there is no battery"),
            Q(8, "Która planeta jest najgorętsza?", "Which planet is the hottest?", "Wenus|Merkury|Mars", "Venus|Mercury|Mars"),
            Q(8, "Jak nazywa się zamiana pary wodnej w krople wody, np. na zimnej szybie?", "What is it called when water vapour turns into drops, for example on a cold window?", "skraplanie|topnienie|parowanie", "condensation|melting|evaporation"),
            Q(9, "Ile mniej więcej leci światło ze Słońca do Ziemi?", "About how long does light take to travel from the Sun to the Earth?", "około 8 minut|około 8 sekund|około 8 dni", "about 8 minutes|about 8 seconds|about 8 days"),
            Q(9, "Jak szybko w przybliżeniu porusza się światło?", "About how fast does light travel?", "300 000 km na sekundę|300 km na godzinę|3000 km na sekundę", "300,000 km per second|300 km per hour|3,000 km per second"),
            Q(9, "Na której planecie jest Olympus Mons, najwyższy znany wulkan w Układzie Słonecznym?", "On which planet is Olympus Mons, the highest known volcano in the Solar System?", "na Marsie|na Wenus|na Jowiszu", "Mars|Venus|Jupiter"),
            Q(9, "Kto jako pierwszy zobaczył przez lunetę księżyce Jowisza?", "Who was the first to see Jupiter's moons through a telescope?", "Galileusz|Mikołaj Kopernik|Izaak Newton", "Galileo|Nicolaus Copernicus|Isaac Newton"),
            Q(9, "Z którym uczonym wiąże się opowieść o spadającym jabłku i grawitacji?", "Which scientist is linked to the story of the falling apple and gravity?", "Izaak Newton|Mikołaj Kopernik|Albert Einstein", "Isaac Newton|Nicolaus Copernicus|Albert Einstein"),
            Q(9, "Co jest przyczyną pór roku na Ziemi?", "What causes the seasons on Earth?", "nachylenie osi Ziemi|zmiana odległości od Księżyca|gaśnięcie Słońca zimą", "the tilt of the Earth's axis|the distance to the Moon changing|the Sun getting weaker in winter"),
            Q(9, "Którego gazu jest najwięcej w powietrzu?", "Which gas is there most of in the air?", "azotu|tlenu|dwutlenku węgla", "nitrogen|oxygen|carbon dioxide"),
            Q(9, "Który Polak poleciał w 2025 roku na Międzynarodową Stację Kosmiczną?", "Which Pole flew to the International Space Station in 2025?", "Sławosz Uznański-Wiśniewski|Mirosław Hermaszewski|Robert Lewandowski", "Sławosz Uznański-Wiśniewski|Mirosław Hermaszewski|Robert Lewandowski"),
        };

        static readonly Item[] Kornik = Geography.Concat(Nature).Concat(MusicArt).Where(i => i.Kornik).ToArray();

        static readonly Item[][] Lists = { null, null, Nature, Geography, null, null, TimeList, MusicArt, Health, Science };
    }
}
