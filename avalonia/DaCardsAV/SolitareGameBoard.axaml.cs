using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaCards;

namespace DaCardsAV;

public partial class SolitareGameBoard : UserControl
{
    // Reference dimensions for calculating scale
    private const double ReferenceWidth = 640.0;
    private const double ReferenceHeight = 480.0;
    private const int MaxUndoStates = 100;

    // Dynamic layout properties based on actual canvas size
    private double CanvasWidth => GameCanvas.Bounds.Width > 0 ? GameCanvas.Bounds.Width : ReferenceWidth;
    private double CanvasHeight => GameCanvas.Bounds.Height > 0 ? GameCanvas.Bounds.Height : ReferenceHeight;
    private double Scale => Math.Min(CanvasWidth / ReferenceWidth, CanvasHeight / ReferenceHeight);

    private double CardWidth => 71 * Scale;
    private double CardHeight => 96 * Scale;
    private double CardSpacingX => 85 * Scale;
    private double TableauCardOverlap => 20 * Scale;
    private double FaceUpCardOverlap => 25 * Scale;
    private double BoardMargin => 20 * Scale;
    private double TopRowY => BoardMargin;
    private double TableauY => TopRowY + CardHeight + 30 * Scale;

    private SolitaireGame _game = new();
    private readonly Dictionary<string, Bitmap> _cardImages = new();
    private Bitmap? _cardBack;
    private Bitmap? _foundationPlaceholder;
    private Bitmap? _stockEmpty;
    private Bitmap? _stockRestart;

    // Drag state
    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _dragOffset;
    private readonly List<Image> _draggedCards = new();
    private DragSource _dragSource;
    private int _dragSourceTableauIndex = -1;
    private int _dragSourceCardIndex = -1;

    // Undo/Redo stacks
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    // Win animation state
    private bool _isAnimatingWin;
    private DispatcherTimer? _animationTimer;
    private readonly List<BouncingCard> _bouncingCards = new();
    private readonly Queue<Bitmap> _cardsToLaunch = new();
    private int _launchDelay;
    private readonly Random _random = new();

    // Auto-move animation state
    private bool _isAnimatingAutoMove;
    private AutoMoveAnimation? _currentAutoMove;

    private enum DragSource { None, Waste, Tableau }

    // Class to track bouncing card physics
    private class BouncingCard
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public Bitmap Image { get; }

        public BouncingCard(Bitmap image, double x, double y, double vx, double vy)
        {
            Image = image;
            X = x;
            Y = y;
            VelocityX = vx;
            VelocityY = vy;
        }
    }

    private class AutoMoveAnimation
    {
        public required Bitmap Image { get; init; }
        public required double StartX { get; init; }
        public required double StartY { get; init; }
        public required double EndX { get; init; }
        public required double EndY { get; init; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public required Action OnComplete { get; init; }
    }

    public SolitareGameBoard()
    {
        InitializeComponent();
        LoadCardImages();
        _game.NewGame();

        Loaded += (_, _) =>
        {
            RenderGame();
            TryAutoMoveToFoundations();
        };

        // Re-render when canvas size changes
        GameCanvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty && !_isDragging && !_isAnimatingWin && !_isAnimatingAutoMove)
            {
                RenderGame();
            }
        };

        // Set up canvas for drag tracking
        GameCanvas.PointerMoved += OnPointerMoved;
        GameCanvas.PointerReleased += OnPointerReleased;
        GameCanvas.Background = Avalonia.Media.Brushes.Transparent; // Ensure hit testing works
    }

    public bool CanUndo => _undoStack.Count > 0 && !_isAnimatingWin && !_isAnimatingAutoMove;
    public bool CanRedo => _redoStack.Count > 0 && !_isAnimatingWin && !_isAnimatingAutoMove;

    private void SaveState()
    {
        _undoStack.Push(_game.SaveGame());
        _redoStack.Clear(); // Clear redo stack when a new action is performed

        // Limit undo stack size
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

        // Save current state to redo stack
        _redoStack.Push(_game.SaveGame());

        // Restore previous state
        var previousState = _undoStack.Pop();
        if (SolitaireGame.LoadGame(previousState) is SolitaireGame loadedGame)
        {
            _game = loadedGame;
        }

        RenderGame();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        // Save current state to undo stack
        _undoStack.Push(_game.SaveGame());

        // Restore next state
        var nextState = _redoStack.Pop();
        if (SolitaireGame.LoadGame(nextState) is SolitaireGame loadedGame)
        {
            _game = loadedGame;
        }

        RenderGame();
    }

    private void LoadCardImages()
    {
        // Load card back (using first deck design)
        _cardBack = LoadBitmap("avares://DaCardsAV/Assets/decks/deck_1.png");
        _foundationPlaceholder = LoadBitmap("avares://DaCardsAV/Assets/misc/foundation.png");
        _stockEmpty = LoadBitmap("avares://DaCardsAV/Assets/misc/talon_end.png");
        _stockRestart = LoadBitmap("avares://DaCardsAV/Assets/misc/talon_restart.png");

        // Load all card faces
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

        RenderStock();
        RenderWaste();
        RenderFoundations();
        RenderTableaus();
    }

    private void RenderStock()
    {
        var x = BoardMargin;
        var y = TopRowY;

        Image stockImage;
        if (_game.Stock.IsEmpty)
        {
            stockImage = CreateImage(_game.Waste.IsEmpty ? _stockEmpty : _stockRestart, x, y);
        }
        else
        {
            stockImage = CreateImage(_cardBack, x, y);
        }

        stockImage.Cursor = new Cursor(StandardCursorType.Hand);
        stockImage.PointerPressed += OnStockClicked;
        GameCanvas.Children.Add(stockImage);
    }

    private void RenderWaste()
    {
        var x = BoardMargin + CardSpacingX;
        var y = TopRowY;

        if (!_game.Waste.IsEmpty)
        {
            var topCard = _game.Waste.TopCard!;
            var cardImage = CreateImage(GetCardImage(topCard), x, y);
            cardImage.Cursor = new Cursor(StandardCursorType.Hand);
            cardImage.Tag = ("waste", topCard);
            cardImage.PointerPressed += OnWasteCardPressed;
            GameCanvas.Children.Add(cardImage);
        }
    }

    private void RenderFoundations()
    {
        for (int i = 0; i < 4; i++)
        {
            var x = BoardMargin + (3 + i) * CardSpacingX;
            var y = TopRowY;
            var foundation = _game.Foundations[i];

            Image image;
            if (foundation.IsEmpty)
            {
                image = CreateImage(_foundationPlaceholder, x, y);
            }
            else
            {
                image = CreateImage(GetCardImage(foundation.TopCard!), x, y);
            }

            image.Tag = ("foundation", i);
            GameCanvas.Children.Add(image);
        }
    }

    private void RenderTableaus()
    {
        for (int t = 0; t < 7; t++)
        {
            var tableau = _game.Tableaus[t];
            var x = BoardMargin + t * CardSpacingX;
            var y = TableauY;

            // Always render a drop target area for empty tableaus
            var dropTarget = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                BorderBrush = Avalonia.Media.Brushes.DarkGreen,
                BorderThickness = new Thickness(tableau.IsEmpty ? 2 : 0),
                CornerRadius = new CornerRadius(5),
                Tag = ("tableau_drop", t),
                Background = Avalonia.Media.Brushes.Transparent
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
        if (_isDragging || _isAnimatingWin || _isAnimatingAutoMove) return;

        SaveState();

        if (_game.Stock.IsEmpty)
        {
            _game.ResetStock();
        }
        else
        {
            _game.DrawFromStock();
        }

        RenderGame();
    }

    private void OnWasteCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || _isAnimatingWin || _isAnimatingAutoMove) return;

        // Double-click to auto-move to foundation
        if (e.ClickCount == 2)
        {
            SaveState();
            _game.AutoMoveWasteToFoundation();
            RenderGame();
            CheckForWin();
            TryAutoMoveToFoundations();
            return;
        }

        // Start drag
        var topCard = _game.Waste.TopCard!;
        _isDragging = true;
        _dragSource = DragSource.Waste;
        _dragStartPoint = e.GetPosition(GameCanvas);
        _dragOffset = e.GetPosition(image);

        // Create dragged card visual
        var dragImage = CreateImage(GetCardImage(topCard), _dragStartPoint.X - _dragOffset.X, _dragStartPoint.Y - _dragOffset.Y);
        dragImage.Opacity = 0.8;
        dragImage.IsHitTestVisible = false;
        dragImage.ZIndex = 1000;
        _draggedCards.Add(dragImage);
        GameCanvas.Children.Add(dragImage);

        // Hide original card
        image.Opacity = 0.3;

        e.Pointer.Capture(GameCanvas);
    }

    private void OnTableauCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || image.Tag is not (string _, int tableauIndex, int cardIndex, Card _))
            return;

        if (_isAnimatingWin || _isAnimatingAutoMove) return;

        var tableau = _game.Tableaus[tableauIndex];

        // Double-click to auto-move to foundation
        if (e.ClickCount == 2)
        {
            if (cardIndex == tableau.Count - 1)
            {
                SaveState();
                _game.AutoMoveToFoundation(tableauIndex);
                RenderGame();
                CheckForWin();
                TryAutoMoveToFoundations();
                return;
            }
        }

        // Start drag
        _isDragging = true;
        _dragSource = DragSource.Tableau;
        _dragSourceTableauIndex = tableauIndex;
        _dragSourceCardIndex = cardIndex;
        _dragStartPoint = e.GetPosition(GameCanvas);
        _dragOffset = e.GetPosition(image);

        // Create dragged card visuals for all cards from cardIndex to end
        for (int i = cardIndex; i < tableau.Count; i++)
        {
            var card = tableau.Cards[i];
            var yOffset = (i - cardIndex) * FaceUpCardOverlap;
            var dragImage = CreateImage(GetCardImage(card), _dragStartPoint.X - _dragOffset.X, _dragStartPoint.Y - _dragOffset.Y + yOffset);
            dragImage.Opacity = 0.8;
            dragImage.IsHitTestVisible = false;
            dragImage.ZIndex = 1000 + i;
            _draggedCards.Add(dragImage);
            GameCanvas.Children.Add(dragImage);
        }

        // Dim original cards
        foreach (var child in GameCanvas.Children)
        {
            if (child is Image img && img.Tag is (string type, int tIdx, int cIdx, Card _)
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
        if (!_isDragging)
        {
            return;
        }

        e.Pointer.Capture(null);
        var dropPos = e.GetPosition(GameCanvas);

        // Determine drop target and execute move if valid
        var dropTarget = FindDropTarget(dropPos);
        if (dropTarget != null)
        {
            ExecuteDrop(dropTarget.Value);
        }

        // Clean up drag state
        _isDragging = false;
        _dragSource = DragSource.None;
        _dragSourceTableauIndex = -1;
        _dragSourceCardIndex = -1;

        // Remove drag visuals and re-render
        foreach (var dragImage in _draggedCards)
        {
            GameCanvas.Children.Remove(dragImage);
        }
        _draggedCards.Clear();

        RenderGame();
        CheckForWin();
        TryAutoMoveToFoundations();
    }

    private (string Type, int Index)? FindDropTarget(Point pos)
    {
        // Check foundations (only for single card drops)
        if (_draggedCards.Count == 1)
        {
            for (int i = 0; i < 4; i++)
            {
                var x = BoardMargin + (3 + i) * CardSpacingX;
                var y = TopRowY;
                if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= y && pos.Y <= y + CardHeight)
                {
                    return ("foundation", i);
                }
            }
        }

        // Check tableaus
        for (int t = 0; t < 7; t++)
        {
            var x = BoardMargin + t * CardSpacingX;
            var tableau = _game.Tableaus[t];

            // Calculate tableau bounds (including stacked cards)
            var tableauTop = TableauY;
            var tableauBottom = tableau.IsEmpty
                ? TableauY + CardHeight
                : TableauY + (tableau.Count - 1) * FaceUpCardOverlap + CardHeight;

            if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= tableauTop && pos.Y <= tableauBottom)
            {
                return ("tableau", t);
            }
        }

        return null;
    }

    private void ExecuteDrop((string Type, int Index) target)
    {
        bool moved = false;

        switch (_dragSource)
        {
            case DragSource.Waste:
                if (target.Type == "foundation")
                {
                    SaveState();
                    moved = _game.MoveWasteToFoundation(target.Index);
                }
                else if (target.Type == "tableau")
                {
                    SaveState();
                    moved = _game.MoveWasteToTableau(target.Index);
                }
                break;

            case DragSource.Tableau:
                if (target.Type == "foundation" && _draggedCards.Count == 1)
                {
                    SaveState();
                    moved = _game.MoveTableauToFoundation(_dragSourceTableauIndex, target.Index);
                }
                else if (target.Type == "tableau" && target.Index != _dragSourceTableauIndex)
                {
                    SaveState();
                    moved = _game.MoveTableauToTableau(_dragSourceTableauIndex, _dragSourceCardIndex, target.Index);
                }
                break;
        }

        // If move failed, remove the state we just saved
        if (!moved && _undoStack.Count > 0)
        {
            _undoStack.Pop();
        }
    }

    private void CheckForWin()
    {
        if (_game.IsGameWon && !_isAnimatingWin)
        {
            StartWinAnimation();
        }
    }

    #region Auto-Move to Foundations

    private void TryAutoMoveToFoundations()
    {
        // Check if auto-move is enabled
        if (!GameSettings.AutoMoveEnabled) return;

        // Skip if already animating
        if (_isAnimatingAutoMove || _isAnimatingWin || _isDragging) return;

        // Find first safe card to auto-move
        var autoMove = FindNextSafeAutoMove();
        if (autoMove == null) return;

        // Start animation
        _isAnimatingAutoMove = true;
        _currentAutoMove = autoMove;
        _currentAutoMove.CurrentX = _currentAutoMove.StartX;
        _currentAutoMove.CurrentY = _currentAutoMove.StartY;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAutoMoveTick;
        _animationTimer.Start();
    }

    private AutoMoveAnimation? FindNextSafeAutoMove()
    {
        // Check waste pile first
        if (!_game.Waste.IsEmpty)
        {
            var card = _game.Waste.TopCard!;
            if (IsSafeToAutoMove(card))
            {
                var foundationIndex = FindAcceptingFoundation(card);
                if (foundationIndex >= 0)
                {
                    var startX = BoardMargin + CardSpacingX;
                    var endX = BoardMargin + (3 + foundationIndex) * CardSpacingX;

                    return new AutoMoveAnimation
                    {
                        Image = GetCardImage(card)!,
                        StartX = startX,
                        StartY = TopRowY,
                        EndX = endX,
                        EndY = TopRowY,
                        OnComplete = () =>
                        {
                            SaveState();
                            _game.MoveWasteToFoundation(foundationIndex);
                        }
                    };
                }
            }
        }

        // Check tableau top cards
        for (int t = 0; t < 7; t++)
        {
            var tableau = _game.Tableaus[t];
            if (tableau.IsEmpty) continue;

            var card = tableau.TopCard!;
            if (IsSafeToAutoMove(card))
            {
                var foundationIndex = FindAcceptingFoundation(card);
                if (foundationIndex >= 0)
                {
                    var startX = BoardMargin + t * CardSpacingX;
                    var startY = TableauY + (tableau.Count - 1) * FaceUpCardOverlap;
                    var endX = BoardMargin + (3 + foundationIndex) * CardSpacingX;
                    var tableauIndex = t;

                    return new AutoMoveAnimation
                    {
                        Image = GetCardImage(card)!,
                        StartX = startX,
                        StartY = startY,
                        EndX = endX,
                        EndY = TopRowY,
                        OnComplete = () =>
                        {
                            SaveState();
                            _game.MoveTableauToFoundation(tableauIndex, foundationIndex);
                        }
                    };
                }
            }
        }

        return null;
    }

    private bool IsSafeToAutoMove(Card card)
    {
        // First check if any foundation can accept this card
        if (FindAcceptingFoundation(card) < 0) return false;

        // Aces are always safe
        if (card.Rank == Rank.Ace) return true;

        // 2s are always safe (aces must already be in foundations)
        if (card.Rank == Rank.Two) return true;

        // For higher cards, check if both opposite-color cards of rank-1 are in foundations
        var neededRank = (int)card.Rank - 1;
        var oppositeColorSuits = card.IsRed
            ? new[] { Suit.Clubs, Suit.Spades }
            : new[] { Suit.Hearts, Suit.Diamonds };

        foreach (var suit in oppositeColorSuits)
        {
            if (!FoundationHasRankOrHigher(suit, neededRank))
                return false;
        }

        return true;
    }

    private bool FoundationHasRankOrHigher(Suit suit, int rank)
    {
        foreach (var foundation in _game.Foundations)
        {
            if (foundation.IsEmpty) continue;
            if (foundation.Suit == suit && (int)foundation.TopCard!.Rank >= rank)
                return true;
        }
        return false;
    }

    private int FindAcceptingFoundation(Card card)
    {
        for (int f = 0; f < 4; f++)
        {
            if (_game.Foundations[f].CanAcceptCard(card))
                return f;
        }
        return -1;
    }

    private void OnAutoMoveTick(object? sender, EventArgs e)
    {
        if (_currentAutoMove == null)
        {
            StopAutoMoveAnimation();
            return;
        }

        const double speed = 60.0; // pixels per frame

        var dx = _currentAutoMove.EndX - _currentAutoMove.CurrentX;
        var dy = _currentAutoMove.EndY - _currentAutoMove.CurrentY;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < speed)
        {
            // Arrived - execute the move
            _currentAutoMove.OnComplete();
            _currentAutoMove = null;

            // Stop current animation
            _animationTimer?.Stop();
            _animationTimer = null;
            _isAnimatingAutoMove = false;

            // Render and check for more auto-moves or win
            RenderGame();
            CheckForWin();

            // Try to find more safe moves (sequential animation)
            if (!_isAnimatingWin)
            {
                TryAutoMoveToFoundations();
            }
        }
        else
        {
            // Move toward destination
            _currentAutoMove.CurrentX += (dx / distance) * speed;
            _currentAutoMove.CurrentY += (dy / distance) * speed;

            // Render game state with animated card overlay
            RenderAutoMoveFrame();
        }
    }

    private void RenderAutoMoveFrame()
    {
        // Render normal game state
        RenderGame();

        // Overlay the animating card on top
        if (_currentAutoMove != null)
        {
            var animCard = new Image
            {
                Source = _currentAutoMove.Image,
                Width = CardWidth,
                Height = CardHeight,
                ZIndex = 1000
            };
            Canvas.SetLeft(animCard, _currentAutoMove.CurrentX);
            Canvas.SetTop(animCard, _currentAutoMove.CurrentY);
            GameCanvas.Children.Add(animCard);
        }
    }

    private void StopAutoMoveAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
        _isAnimatingAutoMove = false;
        _currentAutoMove = null;
    }

    #endregion

    private void StartWinAnimation()
    {
        _isAnimatingWin = true;
        _bouncingCards.Clear();
        _cardsToLaunch.Clear();
        _launchDelay = 0;

        // Queue all cards from foundations (King to Ace, all suits)
        // Launch from each foundation position
        foreach (var foundation in _game.Foundations)
        {
            // Add cards in reverse order (King first) for visual effect
            for (int rank = 13; rank >= 1; rank--)
            {
                var card = foundation.Cards.FirstOrDefault(c => (int)c.Rank == rank);
                if (card != null)
                {
                    var bitmap = GetCardImage(card);
                    if (bitmap != null)
                    {
                        _cardsToLaunch.Enqueue(bitmap);
                    }
                }
            }
        }

        // Clear the canvas and start with green background
        GameCanvas.Children.Clear();

        // Start the animation timer (~60fps)
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
        const int launchInterval = 8; // Frames between card launches

        // Launch new cards periodically
        _launchDelay++;
        if (_launchDelay >= launchInterval && _cardsToLaunch.Count > 0)
        {
            _launchDelay = 0;
            var bitmap = _cardsToLaunch.Dequeue();

            // Determine launch position based on which foundation this card came from
            var foundationIndex = (_cardsToLaunch.Count / 13) % 4;
            var startX = BoardMargin + (3 + foundationIndex) * CardSpacingX;
            var startY = TopRowY;

            // Random horizontal direction
            var vx = (_random.NextDouble() > 0.5 ? 1 : -1) * (horizontalSpeed + _random.NextDouble() * 2);
            var vy = _random.NextDouble() * 2; // Small initial downward velocity

            _bouncingCards.Add(new BouncingCard(bitmap, startX, startY, vx, vy));
        }

        // Update physics and render trails for each bouncing card
        var cardsToRemove = new List<BouncingCard>();

        foreach (var card in _bouncingCards)
        {
            // Leave a trail (static copy at current position)
            var trailImage = new Image
            {
                Source = card.Image,
                Width = CardWidth,
                Height = CardHeight
            };
            Canvas.SetLeft(trailImage, card.X);
            Canvas.SetTop(trailImage, card.Y);
            GameCanvas.Children.Add(trailImage);

            // Apply gravity
            card.VelocityY += gravity;

            // Update position
            card.X += card.VelocityX;
            card.Y += card.VelocityY;

            // Bounce off bottom
            if (card.Y + CardHeight >= CanvasHeight)
            {
                card.Y = CanvasHeight - CardHeight;
                card.VelocityY = -card.VelocityY * damping;

                // Stop if velocity is too low
                if (Math.Abs(card.VelocityY) < 1)
                {
                    card.VelocityY = 0;
                }
            }

            // Remove card if it exits horizontally
            if (card.X < -CardWidth || card.X > CanvasWidth)
            {
                cardsToRemove.Add(card);
            }
        }

        // Remove cards that have exited
        foreach (var card in cardsToRemove)
        {
            _bouncingCards.Remove(card);
        }

        // Stop animation when all cards have been launched and exited
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

    public void NewGame(int drawCount = 1, int? seed = null)
    {
        StopWinAnimation();
        StopAutoMoveAnimation();
        _isDragging = false;
        _dragSource = DragSource.None;
        _draggedCards.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _game.DrawCount = drawCount;
        _game.NewGame(seed);
        RenderGame();
        TryAutoMoveToFoundations();
    }

    public bool IsGameWon => _game.IsGameWon;

    public void TriggerWinAnimation()
    {
        if (_isAnimatingWin) return;

        // Set up a fake won state for the animation
        // by collecting all card images from all suits
        _isAnimatingWin = true;
        _bouncingCards.Clear();
        _cardsToLaunch.Clear();
        _launchDelay = 0;

        // Queue all 52 cards (King to Ace for each suit)
        var suits = new[] { Suit.Hearts, Suit.Diamonds, Suit.Clubs, Suit.Spades };
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

        // Clear the canvas
        GameCanvas.Children.Clear();

        // Start the animation timer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
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
