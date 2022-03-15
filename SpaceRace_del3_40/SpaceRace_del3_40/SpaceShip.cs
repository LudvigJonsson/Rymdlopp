using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

namespace SpaceRace_del3_40
{
    class SpaceShip
    {
        Texture2D _gfx, _gfx_acc, _gfx_explosion;
        bool _accelerating;
        float _turnRate, _rotation;
        Vector2 _position, _speed;

        bool _recalcHull = true;
        List<Point> _hull;
        Dictionary<Point, bool> _hullTransformed;

        bool _collision = false;

        float _shield;
        float _fuel;
        bool _takingDamage = false;

        Cue throttleSound;

        bool exploding = false;
        bool _dead = false;
        TimeSpan anim_time = new TimeSpan();
        int anim_frame = 0;

        #region Properties

        public float Shield
        {
            get { return _shield; }
            set 
            { 
                _shield = value;
                if (_shield < 0)
                    _shield = 0;
            }
        }

        public float Fuel
        {
            get { return _fuel; }
            set
            {
                _fuel = value;
                if (_fuel < 0)
                    _fuel = 0;
            }
        }

        public Vector2 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public Vector2 Speed
        {
            get { return _speed; }
            set { _speed = value; }
        }

        public float TurnRate
        {
            get { return _turnRate; }
            set { _turnRate = value; }
        }

        public float Rotation
        {
            get { return _rotation; }
            set { _rotation = value; _recalcHull = true; }
        }

        public bool Accelerating
        {
            get { return _accelerating; }
            set { _accelerating = value; }
        }

        public bool Dead
        {
            get { return _dead; }
            set { _dead = value; }
        }

        #endregion

        public SpaceShip(Texture2D ship_normal, Texture2D ship_acc, Texture2D explosion)
        {
            this._gfx = ship_normal;
            this._gfx_acc = ship_acc;
            this._gfx_explosion = explosion;
            FindHull();
            Fuel = 100;
            Shield = 10000;

            throttleSound = Game1.soundBank.GetCue("throttle");
        }

        public void Update(GameTime gameTime)
        {
            if (!exploding)
            {
                if (Accelerating && Fuel > 0)
                {
                    Speed += Game1.gravity + Vector2.Transform(new Vector2(0, -0.03f), Matrix.CreateRotationZ(Rotation));
                    Fuel -= 0.1f;

                    //Update sound
                    if (!throttleSound.IsPlaying)
                        throttleSound.Play();
                    else if (throttleSound.IsPaused)
                        throttleSound.Resume();
                }
                else
                {
                    if (throttleSound.IsPlaying)
                        throttleSound.Pause();
                    Accelerating = false;
                    Speed += Game1.gravity;
                }

                if (!_collision)
                    Rotation += _turnRate;

                if (_takingDamage)
                    Shield -= Speed.Length() * 0.0f;

                if (Shield <= 0)
                {
                    Game1.soundBank.PlayCue("explosion");
                    exploding = true;
                    return;
                }

                _position += _speed;

                if (_recalcHull)
                    UpdateHull();
            }
            else
            {
                if (throttleSound.IsPlaying)
                    throttleSound.Pause();

                anim_time += gameTime.ElapsedGameTime;
                if (anim_time.TotalMilliseconds > 50)
                {
                    anim_time = new TimeSpan();
                    anim_frame++;
                    if (anim_frame > 15)
                        Dead = true;
                }
            }
        }

        public void ResetShip()
        {
            anim_frame = 0;
            exploding = false;
            Shield = 10000;
            Fuel = 100;
            Dead = false;
            Rotation = 0;
            TurnRate = 0;
            UpdateHull();
            Speed = new Vector2();
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (!exploding)
            {
                if (_accelerating)
                    spriteBatch.Draw(_gfx_acc, _position, null, Color.White, _rotation,
                        new Vector2(_gfx.Width / 2, _gfx.Height / 2), 1.0f, SpriteEffects.None, 0);
                else
                    spriteBatch.Draw(_gfx, _position, null, Color.White, _rotation,
                        new Vector2(_gfx.Width / 2, _gfx.Height / 2), 1.0f, SpriteEffects.None, 0);
            }
            else
            {
                Rectangle tmp = new Rectangle((anim_frame % 4) * 64, (anim_frame / 4) * 64, 64, 64);
                spriteBatch.Draw(_gfx_explosion, _position, tmp, new Color(255, 255, 255, 127), 0,
                        new Vector2(64 / 2, 64 / 2), 1.0f, SpriteEffects.None, 0);
            }
        }

        public bool PointingUp(float degrees)
        {
            // Vi kollar om skeppet pekar uppåt med +/- degrees grader
            float min = (float)Math.Cos(0 + degrees * Math.PI / 180.0f);
            return Math.Cos(Rotation) >= min;
        }

        const UInt32 _black = 0xFF000000;
        const UInt32 _red = 0xFF0000FF;
        const UInt32 _green = 0xFF00FF00;
        const UInt32 _blue = 0xFFFF0000;

        public bool CheckCollision(Texture2D level)
        {
            uint[] levelPixels = new uint[level.Width * level.Height];
            level.GetData<uint>(levelPixels);
            Dictionary<Point, bool>.Enumerator enumer = _hullTransformed.GetEnumerator();

            //Eftersom vi kommer att gå igenom alla pixlar som kolliderar
            //så behöver extra temporära variabler för att kunna räkna ut
            //ett medelvärde längre fram
            int num_found = 0;
            Vector2 delta_pos = new Vector2();
            float delta_angle = 0;

            bool landed = false;
            _takingDamage = false;

            while (enumer.MoveNext())
            {
                //Beräkna pixelpositon för kantpixeln
                Point p = enumer.Current.Key;
                int x = p.X + (int)Position.X;
                int y = p.Y + (int)Position.Y;

                //Pixlar utanför banan ignoreras
                if (x < 0 || y < 0 || x >= level.Width || y >= level.Height)
                    continue;

                //Endast svarta, gröna och blåa pixlar kan vi kollidera mot
                if ((levelPixels[x + y * level.Width] == _black) ||
                    (levelPixels[x + y * level.Width] == _blue) ||
                    (levelPixels[x + y * level.Width] == _green))
                {

                    //Ska vi ta skada? Jo om vi kolliderar med svart
                    _takingDamage = (levelPixels[x + y * level.Width] == _black);

                    //Vektor från centrum (hävstång)
                    Vector2 c = new Vector2(Position.X - x, Position.Y - y);
                    //Roterad vektor 90 grader moturs
                    Vector2 h = new Vector2(-c.Y, c.X);
                    //Beräkna rotation (skalad skalärprodukt)
                    float rot = (h.X * Speed.X + h.Y * Speed.Y) / 600.0f;
                    delta_angle += rot;

                    //Flytta tillbaka skeppet vid första tecken på kollision
                    //för att förhindra att skeppet fastar
                    if (num_found == 0)
                        Position -= Speed;

                    //Vektor som pekar mot centrum för skeppet
                    //se artikelbild för bättre förklaring
                    Vector2 D = new Vector2(x - (Position.X), y - (Position.Y));
                    D.Normalize();
                    Vector2 V = Speed;
                    float length = Vector2.Dot(D, V);
                    Vector2 pl = length * D;
                    Vector2 np = V - pl;
                    //Flytta isär lite extra i riktning från kollisionen
                    delta_pos -= (V.Length() + 0.05f) * D;

                    //Beräkna dämpning beroende på pixelfärg
                    float dampening = 0.8f;
                    if ((levelPixels[x + y * level.Width] == _green) ||
                    (levelPixels[x + y * level.Width] == _blue))
                        dampening = 0.4f;

                    //Ny hastighet
                    Speed = (-pl + np) * dampening;
                    num_found++;

                    //Kolla om vi har landat på mål
                    if ((levelPixels[x + y * level.Width] == _blue)
                        && Speed.Length() < 0.03f && PointingUp(10))
                        landed = true;
                }
            }
            _collision = num_found > 0;
            //Beräkna medelvärdet för positions/rotations ändring samt rotationshastighet
            //om vi har kolliderar
            if (num_found > 0)
            {
                Position += delta_pos / num_found;
                Rotation += delta_angle / num_found;
                TurnRate = delta_angle / num_found;
            }
            return landed;
        }

        private void FindHull()
        {
            _hull = new List<Point>();
            //Läs in pixeldata från spriten
            uint[] bits = new uint[_gfx.Width * _gfx.Height];
            _gfx.GetData<uint>(bits);

            for (int x = 0; x < _gfx.Width; x++)
            {
                for (int y = 0; y < _gfx.Height; y++)
                {
                    //Skippa genomskinliga pixlar
                    if ((bits[x + y * _gfx.Width] & 0xFF000000) >> 24 <= 20)
                        continue;

                    //Pixlar på kanten av texturen?
                    if (x == 0 || y == 0 || x == _gfx.Width - 1 || y == _gfx.Height - 1)
                    {
                        _hull.Add(new Point(x, y));
                        continue;
                    }

                    //Kant i spriten?
                    if (((bits[x + 1 + y * _gfx.Width] & 0xFF000000) >> 24 <= 20) ||
                        ((bits[x - 1 + y * _gfx.Width] & 0xFF000000) >> 24 <= 20) ||
                        ((bits[x + (y + 1) * _gfx.Width] & 0xFF000000) >> 24 <= 20) ||
                        ((bits[x + (y - 1) * _gfx.Width] & 0xFF000000) >> 24 <= 20))
                        _hull.Add(new Point(x, y));
                }
            }
        }

        private void UpdateHull()
        {
            //Initiera med beräknad längd för ökad prestanda
            _hullTransformed = new Dictionary<Point, bool>(_hull.Count);

            //Beräkna rotationen
            float cos = (float)Math.Cos(_rotation);
            float sin = (float)Math.Sin(_rotation);
            //Center för skeppet
            int width = _gfx.Width / 2;
            int height = _gfx.Height / 2;

            foreach (Point p in _hull)
            {
                //Beräkna nytt x o y kring centrum
                int newX = (int)((p.X - width) * cos - (p.Y - height) * sin);
                int newY = (int)((p.Y - height) * cos + (p.X - width) * sin);

                Point newP = new Point(newX, newY);
                //Punkten kan redan finnas p.g.a avrundning
                if (!_hullTransformed.ContainsKey(newP))
                    _hullTransformed.Add(newP, true);
            }

            _recalcHull = false;
        }
    }
}
