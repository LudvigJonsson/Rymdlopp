using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.IO;
using Microsoft.Xna.Framework.Storage;

namespace SpaceRace
{
    enum GameState
    {
        READY,
        PLAYING,
        WON,
        GAMEOVER
    }

    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D level;
        Texture2D ship_normal, ship_acc, explosion;
        SpaceShip ship;

        public static Vector2 gravity = new Vector2(0, 0.01f);
        KeyboardState prev_ks = new KeyboardState();

        TimeSpan levelTime;
        TimeSpan warningTime;
        bool landed = false;
        SpriteFont hudFont;
        Texture2D fuel, fuel_meter;
        int levelIndex = 3;
        AudioEngine audioEngine;
        WaveBank waveBank;
        public static SoundBank soundBank;

        GameState gameState = GameState.READY;
        Texture2D overlay_gameover, overlay_win, overlay_ready;
        SpriteFont largeFont;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 600;
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            audioEngine = new AudioEngine("Content\\Sounds\\sounds.xgs");
            waveBank = new WaveBank(audioEngine, "Content\\Sounds\\Wave Bank.xwb");
            soundBank = new SoundBank(audioEngine, "Content\\Sounds\\Sound Bank.xsb");

            spriteBatch = new SpriteBatch(GraphicsDevice);

            overlay_gameover = Content.Load<Texture2D>("you_died");
            overlay_win = Content.Load<Texture2D>("you_win");
            overlay_ready = Content.Load<Texture2D>("you_ready");
            largeFont = Content.Load<SpriteFont>("largeFont");

            hudFont = Content.Load<SpriteFont>("Hud");
            ship_normal = Content.Load<Texture2D>("ship");
            ship_acc = Content.Load<Texture2D>("ship_acc");
            explosion = Content.Load<Texture2D>("explosion");
            ship = new SpaceShip(ship_normal, ship_acc, explosion);

            warningTime = TimeSpan.FromSeconds(30);
            fuel = Content.Load<Texture2D>("Fuel");
            fuel_meter = Content.Load<Texture2D>("Fuel_meter");
            LoadNextLevel();

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0.05f;
            MediaPlayer.Play(Content.Load<Song>("Sounds/music"));
        }

        protected Vector2 FindPixel(Texture2D texture, uint color)
        {
            //Kopiera pixeldata som uint
            uint[] bits = new uint[texture.Width * texture.Height];
            texture.GetData<uint>(bits);

            for (int x = 0; x < level.Width; x++)
            {
                for (int y = 0; y < level.Height; y++)
                {
                    //Har vi hittat det vi söker?
                    if (bits[x + y * texture.Width] == color)
                        return new Vector2(x, y);
                }
            }
            //Om inget hittades
            return Vector2.Zero;
        }

        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            audioEngine.Update();

            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            //Läs aktuell keyboard status
            KeyboardState ks = Keyboard.GetState();

            switch (gameState)
            {
                case GameState.PLAYING:
                    levelTime -= gameTime.ElapsedGameTime;
                    if (levelTime.TotalSeconds <= 0)
                    {
                        gameState = GameState.GAMEOVER;
                        return;
                    }

                    //Tryckte vi ned några knappar?
                    if ((ks.IsKeyDown(Keys.A) && !prev_ks.IsKeyDown(Keys.A))
                        || (ks.IsKeyDown(Keys.A) && !ks.IsKeyDown(Keys.D)))
                        ship.TurnRate = -0.03f;
                    else if ((ks.IsKeyDown(Keys.D) && !prev_ks.IsKeyDown(Keys.D))
                        || (ks.IsKeyDown(Keys.D) && !ks.IsKeyDown(Keys.A)))
                        ship.TurnRate = 0.03f;
                    else if ((!ks.IsKeyDown(Keys.D) && prev_ks.IsKeyDown(Keys.D)) ||
                        (!ks.IsKeyDown(Keys.A) && prev_ks.IsKeyDown(Keys.A)))
                        ship.TurnRate = 0;

                    if ((ks.IsKeyDown(Keys.F) && !prev_ks.IsKeyDown(Keys.F)))
                    {
                        this.graphics.IsFullScreen = !this.graphics.IsFullScreen;
                        this.graphics.ApplyChanges();
                    }

                    ship.Accelerating = ks.IsDown(Keys.Up);

                    //Låt skeppet uppdatera sig
                    ship.Update(gameTime);
                    //Kolla kollision
                    landed = ship.CheckCollision(level);
                    if (landed)
                    {
                        soundBank.PlayCue("win");
                        gameState = GameState.WON;
                    }
                    if (ship.Dead)
                        gameState = GameState.GAMEOVER;
                    break;
                case GameState.WON:
                    if (ks.IsKeyDown(Keys.Space) && prev_ks.IsKeyUp(Keys.Space))
                    {
                        LoadNextLevel();
                        gameState = GameState.READY;
                    }
                    break;
                case GameState.GAMEOVER:
                    if (ks.IsKeyDown(Keys.Space) && prev_ks.IsKeyUp(Keys.Space))
                    {
                        levelIndex--;
                        LoadNextLevel();
                        gameState = GameState.READY;
                    }
                    break;
                case GameState.READY:
                    if (ks.IsKeyDown(Keys.Space) && prev_ks.IsKeyUp(Keys.Space))
                    {
                        gameState = GameState.PLAYING;
                    }
                    break;
            }

            //Spara undan keyboard status, som blir föregående status nästa vända
            prev_ks = ks;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();

            //Rita banan
            spriteBatch.Draw(level, Vector2.Zero, Color.White);

            switch (gameState)
            {
                case GameState.PLAYING:
                    //Rita skeppet
                    ship.Draw(gameTime, spriteBatch);

                    //Rita tid etc.
                    DrawHud();
                    break;
                case GameState.WON:
                    Vector2 pos = new Vector2(graphics.GraphicsDevice.Viewport.Width / 2,
                        graphics.GraphicsDevice.Viewport.Height / 2);
                    pos -= new Vector2(overlay_win.Width / 2, overlay_win.Height / 2);
                    spriteBatch.Draw(overlay_win, pos, Color.White);
                    int totalScore = (int)((ship.Fuel + ship.Shield + levelTime.TotalSeconds) * 10);
                    spriteBatch.DrawString(largeFont, totalScore + " points", pos + new Vector2(115, 95), Color.Yellow);
                    spriteBatch.DrawString(hudFont, levelTime.TotalSeconds.ToString("0.0"), pos + new Vector2(55, 140), Color.Yellow);
                    spriteBatch.DrawString(hudFont, ship.Fuel.ToString("0.0"), pos + new Vector2(165, 140), Color.Yellow);
                    spriteBatch.DrawString(hudFont, ship.Shield.ToString("0.0"), pos + new Vector2(275, 140), Color.Yellow);
                    break;
                case GameState.GAMEOVER:
                    pos = new Vector2(graphics.GraphicsDevice.Viewport.Width / 2,
                        graphics.GraphicsDevice.Viewport.Height / 2);
                    pos -= new Vector2(overlay_gameover.Width / 2, overlay_gameover.Height / 2);
                    spriteBatch.Draw(overlay_gameover, pos, Color.White);
                    break;
                case GameState.READY:
                    ship.Draw(gameTime, spriteBatch);
                    DrawHud();

                    pos = new Vector2(graphics.GraphicsDevice.Viewport.Width / 2,
                        graphics.GraphicsDevice.Viewport.Height / 2);
                    pos -= new Vector2(overlay_ready.Width / 2, overlay_ready.Height / 2);
                    spriteBatch.Draw(overlay_ready, pos, Color.White);
                    break;
            }

            spriteBatch.End();
            base.Draw(gameTime);
        }

        private void LoadNextLevel()
        {
            levelIndex++;
            levelTime = TimeSpan.FromMinutes(2);

            string levelFile = "Level" + levelIndex.ToString("00");
            string levelPath = "Content/" + levelFile + ".xnb";
            if (File.Exists(levelPath))
                level = Content.Load<Texture2D>(levelFile);
            else
            {
                levelIndex = 1;
                level = Content.Load<Texture2D>("Level01");
            }
            //Nollställ skeppet
            ship.Position = FindPixel(level, 0xFF0000FF) - new Vector2(0, ship_normal.Height / 2 + 3);
            ship.ResetShip();
        }

        private void DrawHud()
        {
            string timeString = "TIME: " + levelTime.Minutes.ToString("00") + ":" + levelTime.Seconds.ToString("00");
            Color timeColor;
            if (levelTime > warningTime ||
                landed || (int)levelTime.TotalSeconds % 2 == 0)
                timeColor = Color.Yellow;
            else
                timeColor = Color.Red;

            spriteBatch.DrawString(hudFont, timeString, new Vector2(5, 10) + new Vector2(2.0f, 2.0f), Color.Black);
            spriteBatch.DrawString(hudFont, timeString, new Vector2(5, 10), timeColor);

            //Visa röd färg om skäldar < 50%
            Color shieldColor = Color.Yellow;
            if (ship.Shield < 50)
                shieldColor = Color.Red;
            string shieldString = "SHIELDS: " + ship.Shield.ToString("0.0") + "%";
            spriteBatch.DrawString(hudFont, shieldString, new Vector2(5, 30) + new Vector2(2.0f, 2.0f), Color.Black);
            spriteBatch.DrawString(hudFont, shieldString, new Vector2(5, 30), shieldColor);

            // - 160 grader -> slut på bränsle
            // - 20 -> full tank
            float angle = (float)(-160 * Math.PI / 180.0f);
            // 100% tank ger + 140 grader på mätaren
            angle += ship.Fuel / 100.0f * (float)(140 * Math.PI / 180.0f);

            spriteBatch.Draw(fuel, new Vector2(350, 530), Color.White);
            spriteBatch.Draw(fuel_meter, new Vector2(400, 585), null, Color.White, angle,
                    new Vector2(0, 3), 1.0f, SpriteEffects.None, 0);
        }
    }
}
