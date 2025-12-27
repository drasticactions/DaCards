using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaCards;

namespace DaCardsAV;

public partial class SpiderSolitareGameBoard : UserControl
{
    // Reference dimensions for calculating scale
    private const double ReferenceWidth = 900.0;
    private const double ReferenceHeight = 600.0;
    private const int MaxUndoStates = 100;

    // Dynamic layout properties based on actual canvas size
    private double CanvasWidth => GameCanvas.Bounds.Width > 0 ? GameCanvas.Bounds.Width : ReferenceWidth;
    private double CanvasHeight => GameCanvas.Bounds.Height > 0 ? GameCanvas.Bounds.Height : ReferenceHeight;
    private double Scale => Math.Min(CanvasWidth / ReferenceWidth, CanvasHeight / ReferenceHeight);

    private double CardWidth => 71 * Scale;
    private double CardHeight => 96 * Scale;
    private double CardSpacingX => 85 * Scale;
    private double TableauCardOverlap => 18 * Scale;
    private double FaceUpCardOverlap => 22 * Scale;
    private double BoardMargin => 20 * Scale;
    private double TableauY => 10 * Scale;

    // Stock position (bottom right like Windows ME/XP)
    private double StockX => CanvasWidth - CardWidth - BoardMargin;
    private double StockY => CanvasHeight - CardHeight - 20 * Scale;

    // Score panel position (bottom center like Windows ME/XP)
    private double ScorePanelWidth => 140 * Scale;
    private double ScorePanelHeight => 50 * Scale;
    private double ScorePanelX => (CanvasWidth - ScorePanelWidth) / 2;
    private double ScorePanelY => CanvasHeight - ScorePanelHeight - 20 * Scale;

    private SpiderSolitaireGame _game = new();
    private SpiderDifficulty _currentDifficulty = SpiderDifficulty.OneSuit;
    private readonly Dictionary<string, Bitmap> _cardImages = [];
    private Bitmap? _cardBack;
    private Bitmap? _stockEmpty;

    // Drag state
    private bool _isDragging;
    private Point _dragOffset;
    private readonly List<Image> _draggedCards = [];
    private int _dragSourceTableauIndex = -1;
    private int _dragSourceCardIndex = -1;

    // Undo/Redo stacks
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    // Score and moves tracking
    private int _score;
    private int _moves;

    // Win animation state
    private bool _isAnimatingWin;
    private DispatcherTimer? _animationTimer;
    private readonly List<BouncingCard> _bouncingCards = [];
    private readonly Queue<Bitmap> _cardsToLaunch = new();
    private int _launchDelay;
    private readonly Random _random = new();

    private class BouncingCard(Bitmap image, double x, double y, double vx, double vy)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double VelocityX { get; set; } = vx;
        public double VelocityY { get; set; } = vy;
        public Bitmap Image { get; } = image;
    }

    public SpiderSolitareGameBoard()
    {
        InitializeComponent();
        LoadCardImages();
        _game.NewGame(_currentDifficulty);

        Loaded += (_, _) => RenderGame();

        // Re-render when canvas size changes
        GameCanvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty && !_isDragging && !_isAnimatingWin)
            {
                RenderGame();
            }
        };

        GameCanvas.PointerMoved += OnPointerMoved;
        GameCanvas.PointerReleased += OnPointerReleased;
        GameCanvas.Background = Brushes.Transparent;
    }

    public bool CanUndo => _undoStack.Count > 0 && !_isAnimatingWin;
    public bool CanRedo => _redoStack.Count > 0 && !_isAnimatingWin;

    private void SaveState()
    {
        _undoStack.Push(_game.SaveGame());
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoStates)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < MaxUndoStates; i++)
            {
                _undoStack.Push(temp[MaxUndoStates - 1 - i]);
            }
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;

        _redoStack.Push(_game.SaveGame());

        var previousState = _undoStack.Pop();
        if (SpiderSolitaireGame.LoadGame(previousState) is SpiderSolitaireGame loadedGame)
        {
            _game = loadedGame;
            _moves = Math.Max(0, _moves - 1);
        }

        RenderGame();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        _undoStack.Push(_game.SaveGame());

        var nextState = _redoStack.Pop();
        if (SpiderSolitaireGame.LoadGame(nextState) is SpiderSolitaireGame loadedGame)
        {
            _game = loadedGame;
            _moves++;
        }

        RenderGame();
    }

    private void LoadCardImages()
    {
        _cardBack = LoadBitmap("avares://DaCardsAV/Assets/decks/deck_1.png");
        _stockEmpty = LoadBitmap("avares://DaCardsAV/Assets/misc/talon_end.png");

        var suits = new[] { ("c", Suit.Clubs), ("s", Suit.Spades), ("h", Suit.Hearts), ("d", Suit.Diamonds) };
        foreach (var (prefix, suit) in suits)
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                var key = $"{suit}_{rank}";
                var path = $"avares://DaCardsAV/Assets/cards/{prefix}{rank}.png";
                _cardImages[key] = LoadBitmap(path)!;
            }
        }
    }

    private static Bitmap? LoadBitmap(string uri)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(uri));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private Bitmap? GetCardImage(Card card)
    {
        if (!card.IsFaceUp)
            return _cardBack;

        var key = $"{card.Suit}_{(int)card.Rank}";
        return _cardImages.GetValueOrDefault(key);
    }

    private void RenderGame()
    {
        if (_isAnimatingWin) return;

        GameCanvas.Children.Clear();
        _draggedCards.Clear();

        RenderTableaus();
        RenderStock();
        RenderScorePanel();
    }

    private void RenderTableaus()
    {
        for (int t = 0; t < 10; t++)
        {
            var tableau = _game.Tableaus[t];
            var x = BoardMargin + t * CardSpacingX;
            var y = TableauY;

            var dropTarget = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                BorderBrush = Brushes.DarkGreen,
                BorderThickness = new Thickness(tableau.IsEmpty ? 2 : 0),
                CornerRadius = new CornerRadius(5),
                Tag = ("tableau_drop", t),
                Background = Brushes.Transparent
            };
            Canvas.SetLeft(dropTarget, x);
            Canvas.SetTop(dropTarget, y);
            GameCanvas.Children.Add(dropTarget);

            if (!tableau.IsEmpty)
            {
                for (int c = 0; c < tableau.Count; c++)
                {
                    var card = tableau.Cards[c];
                    var cardY = y + c * (card.IsFaceUp ? FaceUpCardOverlap : TableauCardOverlap);
                    var cardImage = CreateImage(GetCardImage(card), x, cardY);

                    if (card.IsFaceUp)
                    {
                        cardImage.Cursor = new Cursor(StandardCursorType.Hand);
                        cardImage.Tag = ("tableau", t, c, card);
                        cardImage.PointerPressed += OnTableauCardPressed;
                    }

                    GameCanvas.Children.Add(cardImage);
                }
            }
        }
    }

    private void RenderStock()
    {
        if (_game.Stock.Count > 0)
        {
            var dealsRemaining = _game.StockDealsRemaining;
            for (int i = 0; i < Math.Min(dealsRemaining, 5); i++)
            {
                var stockImage = CreateImage(_cardBack, StockX - i * 3, StockY);
                if (i == Math.Min(dealsRemaining, 5) - 1)
                {
                    stockImage.Cursor = new Cursor(StandardCursorType.Hand);
                    stockImage.PointerPressed += OnStockClicked;
                }
                GameCanvas.Children.Add(stockImage);
            }
        }
        else
        {
            var emptyImage = CreateImage(_stockEmpty, StockX, StockY);
            GameCanvas.Children.Add(emptyImage);
        }
    }

    private void RenderScorePanel()
    {
        // Create the score panel like Windows ME/XP Spider Solitaire
        // Green bordered box with Score and Moves
        var panel = new Border
        {
            Width = ScorePanelWidth,
            Height = ScorePanelHeight,
            Background = new SolidColorBrush(Color.Parse("#2D7D2D")), // Slightly lighter green
            BorderBrush = new SolidColorBrush(Color.Parse("#1A5C1A")), // Darker green border
            BorderThickness = new Thickness(2 * Scale),
            CornerRadius = new CornerRadius(0)
        };

        var stackPanel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var scoreText = new TextBlock
        {
            Text = $"Score: {_score}",
            Foreground = Brushes.White,
            FontSize = 12 * Scale,
            FontWeight = FontWeight.Normal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 2 * Scale, 0, 0)
        };

        var movesText = new TextBlock
        {
            Text = $"Moves: {_moves}",
            Foreground = Brushes.White,
            FontSize = 12 * Scale,
            FontWeight = FontWeight.Normal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 2 * Scale, 0, 2 * Scale)
        };

        stackPanel.Children.Add(scoreText);
        stackPanel.Children.Add(movesText);
        panel.Child = stackPanel;

        Canvas.SetLeft(panel, ScorePanelX);
        Canvas.SetTop(panel, ScorePanelY);
        GameCanvas.Children.Add(panel);
    }

    private Image CreateImage(Bitmap? bitmap, double x, double y)
    {
        var image = new Image
        {
            Source = bitmap,
            Width = CardWidth,
            Height = CardHeight
        };
        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);
        return image;
    }

    private void OnStockClicked(object? sender, PointerPressedEventArgs e)
    {
        if (_isDragging || _isAnimatingWin) return;

        bool allTableausHaveCards = _game.Tableaus.All(t => !t.IsEmpty);
        if (!allTableausHaveCards)
        {
            return;
        }

        SaveState();

        var previousCompleted = _game.CompletedSequences;

        if (_game.DealFromStock())
        {
            _moves++;
            CheckForNewCompletedSequences(previousCompleted);
            RenderGame();
            CheckForWin();
        }
        else
        {
            if (_undoStack.Count > 0)
                _undoStack.Pop();
        }
    }

    private void OnTableauCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || image.Tag is not (string, int tableauIndex, int cardIndex, Card))
            return;

        if (_isAnimatingWin) return;

        var tableau = _game.Tableaus[tableauIndex];

        if (!tableau.CanPickupFrom(cardIndex))
            return;

        _isDragging = true;
        _dragSourceTableauIndex = tableauIndex;
        _dragSourceCardIndex = cardIndex;
        _dragOffset = e.GetPosition(image);

        var startPoint = e.GetPosition(GameCanvas);

        for (int i = cardIndex; i < tableau.Count; i++)
        {
            var card = tableau.Cards[i];
            var yOffset = (i - cardIndex) * FaceUpCardOverlap;
            var dragImage = CreateImage(GetCardImage(card), startPoint.X - _dragOffset.X, startPoint.Y - _dragOffset.Y + yOffset);
            dragImage.Opacity = 0.8;
            dragImage.IsHitTestVisible = false;
            dragImage.ZIndex = 1000 + i;
            _draggedCards.Add(dragImage);
            GameCanvas.Children.Add(dragImage);
        }

        foreach (var child in GameCanvas.Children)
        {
            if (child is Image img && img.Tag is (string type, int tIdx, int cIdx, Card)
                && type == "tableau" && tIdx == tableauIndex && cIdx >= cardIndex)
            {
                img.Opacity = 0.3;
            }
        }

        e.Pointer.Capture(GameCanvas);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _draggedCards.Count == 0) return;

        var pos = e.GetPosition(GameCanvas);
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var dragImage = _draggedCards[i];
            Canvas.SetLeft(dragImage, pos.X - _dragOffset.X);
            Canvas.SetTop(dragImage, pos.Y - _dragOffset.Y + i * FaceUpCardOverlap);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;

        e.Pointer.Capture(null);
        var dropPos = e.GetPosition(GameCanvas);

        var dropTarget = FindDropTarget(dropPos);
        if (dropTarget.HasValue)
        {
            ExecuteDrop(dropTarget.Value);
        }

        _isDragging = false;
        _dragSourceTableauIndex = -1;
        _dragSourceCardIndex = -1;

        foreach (var dragImage in _draggedCards)
        {
            GameCanvas.Children.Remove(dragImage);
        }
        _draggedCards.Clear();

        RenderGame();
        CheckForWin();
    }

    private int? FindDropTarget(Point pos)
    {
        for (int t = 0; t < 10; t++)
        {
            var x = BoardMargin + t * CardSpacingX;
            var tableau = _game.Tableaus[t];

            var tableauTop = TableauY;
            var tableauBottom = tableau.IsEmpty
                ? TableauY + CardHeight
                : TableauY + (tableau.Count - 1) * FaceUpCardOverlap + CardHeight;

            if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= tableauTop && pos.Y <= tableauBottom)
            {
                return t;
            }
        }

        return null;
    }

    private void ExecuteDrop(int targetTableauIndex)
    {
        if (targetTableauIndex == _dragSourceTableauIndex)
            return;

        SaveState();

        var previousCompleted = _game.CompletedSequences;

        bool moved = _game.MoveCards(_dragSourceTableauIndex, _dragSourceCardIndex, targetTableauIndex);

        if (moved)
        {
            _moves++;
            _score--;
            CheckForNewCompletedSequences(previousCompleted);
        }
        else if (_undoStack.Count > 0)
        {
            _undoStack.Pop();
        }
    }

    private void CheckForNewCompletedSequences(int previousCompleted)
    {
        var newCompleted = _game.CompletedSequences - previousCompleted;
        _score += newCompleted * 100;
    }

    private void CheckForWin()
    {
        if (_game.IsGameWon && !_isAnimatingWin)
        {
            StartWinAnimation();
        }
    }

    private void StartWinAnimation()
    {
        _isAnimatingWin = true;
        _bouncingCards.Clear();
        _cardsToLaunch.Clear();
        _launchDelay = 0;

        var suits = _currentDifficulty switch
        {
            SpiderDifficulty.OneSuit => new[] { Suit.Spades },
            SpiderDifficulty.TwoSuits => new[] { Suit.Spades, Suit.Hearts },
            _ => new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds }
        };

        int setsPerSuit = 8 / suits.Length;
        for (int set = 0; set < setsPerSuit; set++)
        {
            foreach (var suit in suits)
            {
                for (int rank = 13; rank >= 1; rank--)
                {
                    var key = $"{suit}_{rank}";
                    if (_cardImages.TryGetValue(key, out var bitmap))
                    {
                        _cardsToLaunch.Enqueue(bitmap);
                    }
                }
            }
        }

        GameCanvas.Children.Clear();

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        const double gravity = 0.5;
        const double damping = 0.85;
        const double horizontalSpeed = 3.0;
        const int launchInterval = 6;

        _launchDelay++;
        if (_launchDelay >= launchInterval && _cardsToLaunch.Count > 0)
        {
            _launchDelay = 0;
            var bitmap = _cardsToLaunch.Dequeue();

            var startX = StockX;
            var startY = StockY;

            var vx = (_random.NextDouble() > 0.5 ? 1 : -1) * (horizontalSpeed + _random.NextDouble() * 2);
            var vy = -(_random.NextDouble() * 5 + 5);

            _bouncingCards.Add(new BouncingCard(bitmap, startX, startY, vx, vy));
        }

        var cardsToRemove = new List<BouncingCard>();

        foreach (var card in _bouncingCards)
        {
            var trailImage = new Image
            {
                Source = card.Image,
                Width = CardWidth,
                Height = CardHeight
            };
            Canvas.SetLeft(trailImage, card.X);
            Canvas.SetTop(trailImage, card.Y);
            GameCanvas.Children.Add(trailImage);

            card.VelocityY += gravity;
            card.X += card.VelocityX;
            card.Y += card.VelocityY;

            if (card.Y + CardHeight >= CanvasHeight)
            {
                card.Y = CanvasHeight - CardHeight;
                card.VelocityY = -card.VelocityY * damping;

                if (Math.Abs(card.VelocityY) < 1)
                {
                    card.VelocityY = 0;
                }
            }

            if (card.X < -CardWidth || card.X > CanvasWidth)
            {
                cardsToRemove.Add(card);
            }
        }

        foreach (var card in cardsToRemove)
        {
            _bouncingCards.Remove(card);
        }

        if (_cardsToLaunch.Count == 0 && _bouncingCards.Count == 0)
        {
            StopWinAnimation();
        }
    }

    private void StopWinAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
        _isAnimatingWin = false;
    }

    public void NewGame(SpiderDifficulty difficulty = SpiderDifficulty.OneSuit, int? seed = null)
    {
        StopWinAnimation();
        _isDragging = false;
        _draggedCards.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _currentDifficulty = difficulty;
        _game.NewGame(difficulty, seed);

        _score = 500;
        _moves = 0;

        RenderGame();
    }

    public bool IsGameWon => _game.IsGameWon;

    public void TriggerWinAnimation()
    {
        if (_isAnimatingWin) return;
        StartWinAnimation();
    }

    public void SetCardBack(int deckNumber)
    {
        var path = $"avares://DaCardsAV/Assets/decks/deck_{deckNumber}.png";
        var newBack = LoadBitmap(path);
        if (newBack != null)
        {
            _cardBack = newBack;
            RenderGame();
        }
    }
}
