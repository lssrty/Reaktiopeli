using Jypeli;
using Jypeli.Assets;
using Jypeli.Controls;
using Jypeli.Widgets;
using System;
using System.Collections.Generic;
using System.Threading;
using Timer = Jypeli.Timer;

/// <summary>
/// @Author: Lasse Räty
/// @Version: 16112019
/// 
/// Reaktionopeutta mittaava peli, missä pyritään painamaan 
/// kiihtyvällä tahdilla välähtäviä nappeja ennen kuin seuraava nappi ehtii syttyä.
/// Jos toinen nappi ehtii syttyä ennen edellisen napin painamista tai jos pelaajan painaa väärää nappia, peli päättyy.
/// 
/// TODO: Jos aikaa: Vaihdettavat näppäimet, pelattavuus hiirellä, Xbox 360 -ohjaimella ja puhelimella.
/// TODO: Taulukko, ks. https://tim.jyu.fi/answers/kurssit/tie/ohj1/2019s/demot/demo7?answerNumber=4&task=matriisiensumma&user=laaaraty
/// </summary>
public class Reaktiopeli : PhysicsGame
{
    private PhysicsObject nappi1;
    private PhysicsObject nappi2;
    private PhysicsObject nappi3;
    private PhysicsObject nappi4;

    private readonly Timer ValaytaNappia = new Timer();
    private readonly Timer KaikkiValkkyy = new Timer();

    private readonly EasyHighScore topLista = new EasyHighScore();

    private IntMeter pistelaskuri;

    private readonly SoundEffect pisteAani = LoadSoundEffect("piste");
    private readonly SoundEffect virheAani = LoadSoundEffect("vaaranappi");

    private bool highScoreAvattu = false; // Kertoo onko high score- ikkuna jo avattu, jotta samoja pisteitä
                                          // ei voi syöttää uudestaan
    public override void Begin()
    {
        LuoPelikone();
        AsetaOhjaimet();
        LuoPistelaskuri();
        AloitaPeli(); // TODO: Aloittaisi pelin mitä vaan nappia painamalla.
    }


    /// <summary>
    /// Luo pelikoneen, missä on napit ja tausta.
    /// </summary>
    public void LuoPelikone()
    {
        Level.Size = Screen.Size;
        Level.BackgroundColor = Color.Black;

        nappi1 = LuoNappi(Color.Green, -360, (Level.Top + Level.Bottom) / 2, 'G');
        nappi2 = LuoNappi(Color.Blue, -120, (Level.Top + Level.Bottom) / 2, 'H');
        nappi3 = LuoNappi(Color.Red, 120, (Level.Top + Level.Bottom) / 2, 'J');
        nappi4 = LuoNappi(Color.Yellow, 360, (Level.Top + Level.Bottom) / 2, 'K');

        ValaytaNappia.Timeout += SytytaSatunnainenNappi;
        KaikkiValkkyy.Timeout += ValkytaKaikkia;

        MultiSelectWindow alkuValikko = new MultiSelectWindow
            ("Paina välähtävää nappia vastaavaa näppäintä\n    " +
            "ennen kuin seuraava nappi ehtii syttyä.\n           Aloita peli painamalla Enter.", "OK");
        Add(alkuValikko);
    }


    /// <summary>
    /// Luo halutun värisen napin haluttuihin koordinaatteihin. Sammutettuna nappi on harmaa.
    /// </summary>
    /// <param name="vari">Sytytetyn napin väri</param>
    /// <param name="x">Napin x-koordinaatti</param>
    /// <param name="y">Napin y-koordinaatti</param>
    /// <param name="nappain">Nappia painavan näppäimen näppäinohje</param>
    public PhysicsObject LuoNappi(Color vari, double x, double y, char nappain)
    {
        PhysicsObject sammutettuNappi = new PhysicsObject(200.0, 200.0)
        {
            Shape = Shape.Circle,
            Color = Color.DarkGray,
            X = x,
            Y = y
        };
        Add(sammutettuNappi);

        PhysicsObject nappi = new PhysicsObject(200.0, 200.0)
        {
            Shape = Shape.Circle,
            Color = vari,
            X = x,
            Y = y
        };
        Add(nappi);

        Label kirjain = new Label("" + nappain)
        {
            X = x,
            Y = y - 150,
            Font = Font.DefaultLargeBold,
            TextColor = Color.White
        };
        Add(kirjain);

        return nappi;
    }


    /// <summary>
    /// Asettaa peliin ohjaimet mm. välkkyvien nappien painamiseksi sekä pelin lopettamiseksi
    /// </summary>
    public void AsetaOhjaimet()
    {
        Keyboard.Listen(Key.G, ButtonState.Pressed, PainaNappia, "Paina vihreää nappia", nappi1);
        Keyboard.Listen(Key.H, ButtonState.Pressed, PainaNappia, "Paina sinistä nappia", nappi2);
        Keyboard.Listen(Key.J, ButtonState.Pressed, PainaNappia, "Paina punaista nappia", nappi3);
        Keyboard.Listen(Key.K, ButtonState.Pressed, PainaNappia, "Paina keltaista nappia", nappi4);

        Keyboard.Listen(Key.F1, ButtonState.Pressed, ShowControlHelp, "Näytä ohjeet");
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
        Keyboard.Listen(Key.Enter, ButtonState.Pressed, AloitaValayttely, "Aloittaa pelin");
    }


    /// <summary>
    /// Luo peliin pistelaskurin.
    /// </summary>
    public void LuoPistelaskuri()
    {
        pistelaskuri = new IntMeter(0);

        Label pisteNaytto = new Label
        {
            X = 0,
            Y = Screen.Top - 200,
            TextColor = Color.Red,
            Font = Font.DefaultHugeBold
        };

        pisteNaytto.BindTo(pistelaskuri);
        Add(pisteNaytto);
    }


    /// <summary>
    /// Sammuttaa painetun napin jos se on päällä, sekä antaa ajoissa tulleesta painalluksesta pisteen
    /// </summary>
    /// <param name="painettava">Nappi, jota painetaan</param>
    public void PainaNappia(PhysicsObject painettava)
    {
        if (painettava.IsVisible == false && ValaytaNappia.Interval < 1)
        {
            PaataPeli();
            return;
        }

        painettava.IsVisible = false;
        if (ValaytaNappia.Interval < 1)
        {
            pistelaskuri.Value += 1;
            pisteAani.Play();
        }
    }


    /// <summary>
    /// Ajastin, joka määrittää milloin uusi nappi syttyy.
    /// </summary>
    public void AloitaPeli()
    {
        ValotPaalle();
        KaikkiValkkyy.Interval = 1.0;
        KaikkiValkkyy.Start();
        ValaytaNappia.Interval = 1.0;
        pistelaskuri.Reset();
        highScoreAvattu = false;
    }


    /// <summary>
    /// Välkyttää kaikkia nappeja samaan aikaan ennen pelin aloittamista
    /// </summary>
    public void ValkytaKaikkia() 
    {
        if (nappi1.IsVisible == true || nappi2.IsVisible == true || nappi3.IsVisible == true || nappi4.IsVisible == true)
        {
            ValotPois();
            return;
        }
        ValotPaalle();
    }


    /// <summary>
    /// Sytyttää kaikki napit
    /// </summary>
    public void ValotPaalle()
    {
        nappi1.IsVisible = true; nappi2.IsVisible = true; nappi3.IsVisible = true; nappi4.IsVisible = true;
    }


    /// <summary>
    /// Sammuttaa kaikki napit
    /// </summary>
    public void ValotPois()
    {
        nappi1.IsVisible = false; nappi2.IsVisible = false; nappi3.IsVisible = false; nappi4.IsVisible = false;
    }


    /// <summary>
    /// Sammuttaa kaikki napit ja aloittaa satunnaisten nappien välkkymisen
    /// </summary>
    public void AloitaValayttely()
    {
        if (ValaytaNappia.Interval == 1.0)
        {
            KaikkiValkkyy.Stop();
            ValotPois();
            ValaytaNappia.Start();
        }
    }


    /// <summary>
    /// Sytyttää satunnaisen napin ja päättää pelin, jos edellinen valo palaa vielä.
    /// </summary>
    public void SytytaSatunnainenNappi()
    {
        // Tämä if-lauseke lopettaa pelin, jos edellistä valoa ei ole ehditty sammuttaa.
       if(nappi1.IsVisible == true || nappi2.IsVisible == true || nappi3.IsVisible == true || nappi4.IsVisible == true)
        {
            PaataPeli();
            return;
        } 
        RandomGen.SelectOne(nappi1, nappi2, nappi3, nappi4).IsVisible = true;
        ValaytaNappia.Interval *= 0.986; //Nopeuttaa joka kierroksella ajastinta, mikä sytyttää uusia nappeja
    }


    /// <summary>
    /// Päättää pelin. Lopettaa satunnaisten valojen syttymisen ja avaa pelin loppuvalikon.
    /// </summary>
    public void PaataPeli()
    {
        AvaaLoppuvalikko(null);
        ValaytaNappia.Stop();
        virheAani.Play();

        //Takaa sen, että pelaajan myöhästyessä napin painalluksesta 2 eri valoa palaa samaan aikaan. Selkeyttää pelin päättymisen syytä.
        PhysicsObject seuraavaSyttyva = RandomGen.SelectOne(nappi1, nappi2, nappi3, nappi4);
        while (seuraavaSyttyva.IsVisible == true)
        {
            seuraavaSyttyva = RandomGen.SelectOne(nappi1, nappi2, nappi3, nappi4);
        }
        seuraavaSyttyva.IsVisible = true;
        return;
    }


    /// <summary>
    /// Avaa parhaiden pisteiden taulukon. Jos pisteet yltävät parhaisiin pisteisiin, ne voi syöttää.
    /// </summary>
    public void ParhaatPisteet()
    {
        if(highScoreAvattu == false) topLista.EnterAndShow(pistelaskuri.Value);
        if(highScoreAvattu == true) topLista.Show();
        highScoreAvattu = true;
        topLista.HighScoreWindow.Closed += AvaaLoppuvalikko;
    }


    /// <summary>
    /// Pelin loppuessa avaa valikon mistä voi valita haluaako pelata uudelleen, nähdä parhaat pisteet vai lopettaa pelin.
    /// </summary>
    public void AvaaLoppuvalikko(Window sender)
    {
        MultiSelectWindow loppuvalikko = new MultiSelectWindow("Peli päättyi!", "Pelaa uudelleen", "Parhaat pisteet", "Lopeta");
        Add(loppuvalikko);

        loppuvalikko.AddItemHandler(0, AloitaPeli);
        loppuvalikko.AddItemHandler(1, ParhaatPisteet);
        loppuvalikko.AddItemHandler(2, Exit);
    }


}
