using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DaCards;

namespace DaCardsAV;

public partial class FreeCellGameBoard : UserControl
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
    private double TableauCardOverlap => 22 * Scale;
    private double BoardMargin => 20 * Scale;

    // Top row layout - Windows 95 style
    private double TopRowY => 15 * Scale;
    private double FreeCellSpacing => CardWidth + 8 * Scale;
    private double FoundationStartX => BoardMargin;
    private double FreeCellStartX => CanvasWidth - BoardMargin - (4 * FreeCellSpacing) + 8 * Scale;
    private double KingIconSize => 40 * Scale;
    private double KingIconX => (CanvasWidth - KingIconSize) / 2;
    private double KingIconY => TopRowY + (CardHeight - KingIconSize) / 2;

    // Tableau layout - centered horizontally
    private double TableauSpacing => 96 * Scale;
    private double TableauTotalWidth => 7 * TableauSpacing + CardWidth; // 8 columns
    private double TableauStartX => (CanvasWidth - TableauTotalWidth) / 2;
    private double TableauY => TopRowY + CardHeight + 25 * Scale;

    private FreeCellGame _game = new();
    private readonly Dictionary<string, Bitmap> _cardImages = [];
    private Bitmap? _cardBack;
    private Bitmap? _kingLeft;
    private Bitmap? _kingRight;

    // Track last foundation drop for king icon direction (true = looking right, false = looking left)
    private bool _kingLookingRight = true;

    // Drag state
    private bool _isDragging;
    private Point _dragOffset;
    private readonly List<Image> _draggedCards = [];
    private DragSource? _dragSource;

    private enum DragSourceType { Tableau, FreeCell }
    private record DragSource(DragSourceType Type, int Index, int CardIndex = 0);

    // Undo/Redo stacks
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    // Win animation state
    private bool _isAnimatingWin;
    private DispatcherTimer? _animationTimer;
    private readonly List<BouncingCard> _bouncingCards = [];
    private readonly Queue<(Bitmap Image, double StartX)> _cardsToLaunch = new();
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

    // Auto-move animation state
    private bool _isAnimatingAutoMove;
    private AutoMoveAnimation? _currentAutoMove;

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

    public FreeCellGameBoard()
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

        GameCanvas.PointerMoved += OnPointerMoved;
        GameCanvas.PointerReleased += OnPointerReleased;
        GameCanvas.Background = Brushes.Transparent;
    }

    public bool CanUndo => _undoStack.Count > 0 && !_isAnimatingWin && !_isAnimatingAutoMove;
    public bool CanRedo => _redoStack.Count > 0 && !_isAnimatingWin && !_isAnimatingAutoMove;

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
        if (FreeCellGame.LoadGame(previousState) is FreeCellGame loadedGame)
        {
            _game = loadedGame;
        }

        RenderGame();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        _undoStack.Push(_game.SaveGame());

        var nextState = _redoStack.Pop();
        if (FreeCellGame.LoadGame(nextState) is FreeCellGame loadedGame)
        {
            _game = loadedGame;
        }

        RenderGame();
    }

    private void LoadCardImages()
    {
        _cardBack = LoadBitmap("avares://DaCardsAV/Assets/decks/deck_1.png");
        _kingLeft = LoadBitmap("avares://DaCardsAV/Assets/misc/freecell-left-icon.png");
        _kingRight = LoadBitmap("avares://DaCardsAV/Assets/misc/freecell-right-icon.png");

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

        RenderFreeCells();
        RenderKingIcons();
        RenderFoundations();
        RenderTableaus();
        RenderCardsLeftCounter();
    }

    private void RenderFreeCells()
    {
        for (int i = 0; i < FreeCellGame.FreeCellCount; i++)
        {
            var x = FreeCellStartX + i * FreeCellSpacing;
            var card = _game.FreeCells[i];

            // Draw empty cell outline
            var cellBorder = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                BorderBrush = new SolidColorBrush(Color.Parse("#006600")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Background = Brushes.Transparent,
                Tag = ("freecell_drop", i)
            };
            Canvas.SetLeft(cellBorder, x);
            Canvas.SetTop(cellBorder, TopRowY);
            GameCanvas.Children.Add(cellBorder);

            if (card != null)
            {
                var cardImage = CreateImage(GetCardImage(card), x, TopRowY);
                cardImage.Cursor = new Cursor(StandardCursorType.Hand);
                cardImage.Tag = ("freecell", i, card);
                cardImage.PointerPressed += OnFreeCellCardPressed;
                cardImage.DoubleTapped += OnCardDoubleTapped;
                GameCanvas.Children.Add(cardImage);
            }
        }
    }

    private void RenderKingIcons()
    {
        // Single king icon that looks left or right based on last foundation drop
        var kingBitmap = _kingLookingRight ? _kingRight : _kingLeft;
        if (kingBitmap != null)
        {
            var kingIcon = new Image
            {
                Source = kingBitmap,
                Width = KingIconSize,
                Height = KingIconSize
            };

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#006600")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Child = kingIcon
            };

            Canvas.SetLeft(border, KingIconX - 2);
            Canvas.SetTop(border, KingIconY - 2);
            GameCanvas.Children.Add(border);
        }
    }

    // Suit symbols and colors for foundation display
    private static readonly string[] FoundationSuitSymbols = ["♦", "♣", "♥", "♠"];
    private static readonly bool[] FoundationSuitIsRed = [true, false, true, false];

    private void RenderFoundations()
    {
        for (int i = 0; i < FreeCellGame.FoundationCount; i++)
        {
            var x = FoundationStartX + i * FreeCellSpacing;
            var foundation = _game.Foundations[i];

            // Draw empty foundation outline
            var foundationBorder = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                BorderBrush = new SolidColorBrush(Color.Parse("#006600")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Background = Brushes.Transparent,
                Tag = ("foundation_drop", i)
            };
            Canvas.SetLeft(foundationBorder, x);
            Canvas.SetTop(foundationBorder, TopRowY);
            GameCanvas.Children.Add(foundationBorder);

            if (!foundation.IsEmpty)
            {
                // Show top card of foundation
                var topCard = foundation.TopCard!;
                var cardImage = CreateImage(GetCardImage(topCard), x, TopRowY);
                GameCanvas.Children.Add(cardImage);
            }
            else
            {
                // Draw faded suit symbol in empty foundation
                var suitColor = FoundationSuitIsRed[i]
                    ? Color.FromArgb(100, 180, 0, 0)    // Faded red
                    : Color.FromArgb(100, 0, 80, 0);   // Faded dark green (for black suits)

                var suitText = new TextBlock
                {
                    Text = FoundationSuitSymbols[i],
                    FontSize = 36 * Scale,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(suitColor),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                // Center the suit symbol in the foundation
                var suitContainer = new Border
                {
                    Width = CardWidth,
                    Height = CardHeight,
                    Child = suitText
                };
                Canvas.SetLeft(suitContainer, x);
                Canvas.SetTop(suitContainer, TopRowY);
                GameCanvas.Children.Add(suitContainer);
            }
        }
    }

    private void RenderTableaus()
    {
        for (int t = 0; t < FreeCellGame.TableauCount; t++)
        {
            var tableau = _game.Tableaus[t];
            var x = TableauStartX + t * TableauSpacing;
            var y = TableauY;

            // Draw drop target
            var dropTarget = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                BorderBrush = new SolidColorBrush(Color.Parse("#006600")),
                BorderThickness = new Thickness(tableau.IsEmpty ? 2 : 0),
                CornerRadius = new CornerRadius(3),
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
                    var cardY = y + c * TableauCardOverlap;
                    var cardImage = CreateImage(GetCardImage(card), x, cardY);

                    cardImage.Cursor = new Cursor(StandardCursorType.Hand);
                    cardImage.Tag = ("tableau", t, c, card);
                    cardImage.PointerPressed += OnTableauCardPressed;

                    // Only allow double-tap on top card or valid sequence
                    if (c == tableau.Count - 1)
                    {
                        cardImage.DoubleTapped += OnCardDoubleTapped;
                    }

                    GameCanvas.Children.Add(cardImage);
                }
            }
        }
    }

    private void RenderCardsLeftCounter()
    {
        // Calculate cards left (52 - cards in foundations)
        int cardsInFoundations = _game.Foundations.Sum(f => f.Count);
        int cardsLeft = 52 - cardsInFoundations;

        var counterText = new TextBlock
        {
            Text = $"Cards Left: {cardsLeft}",
            Foreground = Brushes.White,
            FontSize = 14 * Scale,
            FontWeight = FontWeight.Bold
        };
        Canvas.SetLeft(counterText, CanvasWidth - 120 * Scale);
        Canvas.SetTop(counterText, CanvasHeight - 30 * Scale);
        GameCanvas.Children.Add(counterText);
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

    private void OnFreeCellCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || image.Tag is not (string, int freeCellIndex, Card))
            return;

        if (_isAnimatingWin || _isAnimatingAutoMove) return;

        var card = _game.FreeCells[freeCellIndex];
        if (card == null) return;

        _isDragging = true;
        _dragSource = new DragSource(DragSourceType.FreeCell, freeCellIndex);
        _dragOffset = e.GetPosition(image);

        var startPoint = e.GetPosition(GameCanvas);
        var dragImage = CreateImage(GetCardImage(card), startPoint.X - _dragOffset.X, startPoint.Y - _dragOffset.Y);
        dragImage.Opacity = 0.8;
        dragImage.IsHitTestVisible = false;
        dragImage.ZIndex = 1000;
        _draggedCards.Add(dragImage);
        GameCanvas.Children.Add(dragImage);

        // Dim original card
        image.Opacity = 0.3;

        e.Pointer.Capture(GameCanvas);
    }

    private void OnTableauCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || image.Tag is not (string, int tableauIndex, int cardIndex, Card))
            return;

        if (_isAnimatingWin || _isAnimatingAutoMove) return;

        var tableau = _game.Tableaus[tableauIndex];
        int maxMovable = _game.GetMaxMovableCards();

        // Check if we can pick up this sequence
        if (!tableau.CanPickupSequence(cardIndex, maxMovable))
            return;

        _isDragging = true;
        _dragSource = new DragSource(DragSourceType.Tableau, tableauIndex, cardIndex);
        _dragOffset = e.GetPosition(image);

        var startPoint = e.GetPosition(GameCanvas);

        for (int i = cardIndex; i < tableau.Count; i++)
        {
            var card = tableau.Cards[i];
            var yOffset = (i - cardIndex) * TableauCardOverlap;
            var dragImage = CreateImage(GetCardImage(card), startPoint.X - _dragOffset.X, startPoint.Y - _dragOffset.Y + yOffset);
            dragImage.Opacity = 0.8;
            dragImage.IsHitTestVisible = false;
            dragImage.ZIndex = 1000 + i;
            _draggedCards.Add(dragImage);
            GameCanvas.Children.Add(dragImage);
        }

        // Dim original cards
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

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_isAnimatingWin || _isAnimatingAutoMove) return;

        Card? card = null;
        int sourceType = -1; // 0 = freecell, 1 = tableau
        int sourceIndex = -1;

        if (sender is Image image)
        {
            if (image.Tag is (string type, int index, Card c) && type == "freecell")
            {
                card = c;
                sourceType = 0;
                sourceIndex = index;
            }
            else if (image.Tag is (string type2, int tIdx, int cIdx, Card c2) && type2 == "tableau")
            {
                var tableau = _game.Tableaus[tIdx];
                if (cIdx == tableau.Count - 1) // Only top card
                {
                    card = c2;
                    sourceType = 1;
                    sourceIndex = tIdx;
                }
            }
        }

        if (card == null) return;

        // Try to auto-move to foundation
        for (int f = 0; f < FreeCellGame.FoundationCount; f++)
        {
            if (_game.Foundations[f].CanAcceptCard(card))
            {
                SaveState();
                bool moved = false;

                if (sourceType == 0)
                {
                    moved = _game.MoveFreeCellToFoundation(sourceIndex, f);
                }
                else if (sourceType == 1)
                {
                    moved = _game.MoveTableauToFoundation(sourceIndex, f);
                }

                if (moved)
                {
                    UpdateKingDirection(f);
                    RenderGame();
                    CheckForWin();
                    TryAutoMoveToFoundations();
                    return;
                }
                else if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                }
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _draggedCards.Count == 0) return;

        var pos = e.GetPosition(GameCanvas);
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var dragImage = _draggedCards[i];
            Canvas.SetLeft(dragImage, pos.X - _dragOffset.X);
            Canvas.SetTop(dragImage, pos.Y - _dragOffset.Y + i * TableauCardOverlap);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging || _dragSource == null) return;

        e.Pointer.Capture(null);
        var dropPos = e.GetPosition(GameCanvas);

        var dropTarget = FindDropTarget(dropPos);
        if (dropTarget != null)
        {
            ExecuteDrop(dropTarget);
        }

        _isDragging = false;
        _dragSource = null;

        foreach (var dragImage in _draggedCards)
        {
            GameCanvas.Children.Remove(dragImage);
        }
        _draggedCards.Clear();

        RenderGame();
        CheckForWin();
        TryAutoMoveToFoundations();
    }

    private enum DropTargetType { Tableau, FreeCell, Foundation }
    private record DropTarget(DropTargetType Type, int Index);

    private DropTarget? FindDropTarget(Point pos)
    {
        // Check free cells
        for (int i = 0; i < FreeCellGame.FreeCellCount; i++)
        {
            var x = FreeCellStartX + i * FreeCellSpacing;
            if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= TopRowY && pos.Y <= TopRowY + CardHeight)
            {
                return new DropTarget(DropTargetType.FreeCell, i);
            }
        }

        // Check foundations
        for (int i = 0; i < FreeCellGame.FoundationCount; i++)
        {
            var x = FoundationStartX + i * FreeCellSpacing;
            if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= TopRowY && pos.Y <= TopRowY + CardHeight)
            {
                return new DropTarget(DropTargetType.Foundation, i);
            }
        }

        // Check tableaus
        for (int t = 0; t < FreeCellGame.TableauCount; t++)
        {
            var x = TableauStartX + t * TableauSpacing;
            var tableau = _game.Tableaus[t];

            var tableauTop = TableauY;
            var tableauBottom = tableau.IsEmpty
                ? TableauY + CardHeight
                : TableauY + (tableau.Count - 1) * TableauCardOverlap + CardHeight;

            if (pos.X >= x && pos.X <= x + CardWidth && pos.Y >= tableauTop && pos.Y <= tableauBottom)
            {
                return new DropTarget(DropTargetType.Tableau, t);
            }
        }

        return null;
    }

    private void ExecuteDrop(DropTarget target)
    {
        if (_dragSource == null) return;

        SaveState();
        bool moved = false;

        switch (_dragSource.Type)
        {
            case DragSourceType.FreeCell:
                moved = ExecuteFreeCellDrop(target);
                break;
            case DragSourceType.Tableau:
                moved = ExecuteTableauDrop(target);
                break;
        }

        if (!moved && _undoStack.Count > 0)
        {
            _undoStack.Pop();
        }
    }

    private bool ExecuteFreeCellDrop(DropTarget target)
    {
        int freeCellIndex = _dragSource!.Index;

        bool moved = target.Type switch
        {
            DropTargetType.Foundation => _game.MoveFreeCellToFoundation(freeCellIndex, target.Index),
            DropTargetType.Tableau => _game.MoveFreeCellToTableau(freeCellIndex, target.Index),
            DropTargetType.FreeCell => false, // Can't move freecell to freecell
            _ => false
        };

        if (moved && target.Type == DropTargetType.Foundation)
        {
            UpdateKingDirection(target.Index);
        }

        return moved;
    }

    private bool ExecuteTableauDrop(DropTarget target)
    {
        int tableauIndex = _dragSource!.Index;
        int cardIndex = _dragSource.CardIndex;

        bool moved = target.Type switch
        {
            DropTargetType.Foundation when _draggedCards.Count == 1 =>
                _game.MoveTableauToFoundation(tableauIndex, target.Index),
            DropTargetType.FreeCell when _draggedCards.Count == 1 =>
                _game.MoveTableauToFreeCell(tableauIndex, target.Index),
            DropTargetType.Tableau =>
                _game.MoveTableauToTableau(tableauIndex, cardIndex, target.Index),
            _ => false
        };

        if (moved && target.Type == DropTargetType.Foundation)
        {
            UpdateKingDirection(target.Index);
        }

        return moved;
    }

    private void UpdateKingDirection(int foundationIndex)
    {
        // King looks toward the half of the foundation area that received the card
        // Foundations 0-1 are on the left half, 2-3 are on the right half
        _kingLookingRight = foundationIndex >= 2;
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
        // Check free cells first
        for (int i = 0; i < FreeCellGame.FreeCellCount; i++)
        {
            var card = _game.FreeCells[i];
            if (card == null) continue;

            if (IsSafeToAutoMove(card))
            {
                var foundationIndex = FindAcceptingFoundation(card);
                if (foundationIndex >= 0)
                {
                    var startX = FreeCellStartX + i * FreeCellSpacing;
                    var endX = FoundationStartX + foundationIndex * FreeCellSpacing;
                    var freeCellIndex = i;

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
                            if (_game.MoveFreeCellToFoundation(freeCellIndex, foundationIndex))
                            {
                                UpdateKingDirection(foundationIndex);
                            }
                        }
                    };
                }
            }
        }

        // Check tableau top cards
        for (int t = 0; t < FreeCellGame.TableauCount; t++)
        {
            var tableau = _game.Tableaus[t];
            if (tableau.IsEmpty) continue;

            var card = tableau.TopCard!;
            if (IsSafeToAutoMove(card))
            {
                var foundationIndex = FindAcceptingFoundation(card);
                if (foundationIndex >= 0)
                {
                    var startX = TableauStartX + t * TableauSpacing;
                    var startY = TableauY + (tableau.Count - 1) * TableauCardOverlap;
                    var endX = FoundationStartX + foundationIndex * FreeCellSpacing;
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
                            if (_game.MoveTableauToFoundation(tableauIndex, foundationIndex))
                            {
                                UpdateKingDirection(foundationIndex);
                            }
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
        // Prefer the foundation that matches the displayed suit symbol
        // Order: Diamonds (0), Clubs (1), Hearts (2), Spades (3)
        var preferredIndex = card.Suit switch
        {
            Suit.Diamonds => 0,
            Suit.Clubs => 1,
            Suit.Hearts => 2,
            Suit.Spades => 3,
            _ => -1
        };

        // Try preferred foundation first
        if (preferredIndex >= 0 && _game.Foundations[preferredIndex].CanAcceptCard(card))
            return preferredIndex;

        // Fall back to any accepting foundation
        for (int f = 0; f < FreeCellGame.FoundationCount; f++)
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
        // Render normal game state (card is already removed visually)
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

        // Queue cards from foundations (King to Ace, from each foundation)
        for (int f = 0; f < FreeCellGame.FoundationCount; f++)
        {
            var foundation = _game.Foundations[f];
            var startX = FoundationStartX + f * FreeCellSpacing;

            for (int c = foundation.Count - 1; c >= 0; c--)
            {
                var card = foundation.Cards[c];
                var key = $"{card.Suit}_{(int)card.Rank}";
                if (_cardImages.TryGetValue(key, out var bitmap))
                {
                    _cardsToLaunch.Enqueue((bitmap, startX));
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
            var (bitmap, startX) = _cardsToLaunch.Dequeue();

            var vx = (_random.NextDouble() > 0.5 ? 1 : -1) * (horizontalSpeed + _random.NextDouble() * 2);
            var vy = -(_random.NextDouble() * 5 + 5);

            _bouncingCards.Add(new BouncingCard(bitmap, startX, TopRowY, vx, vy));
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

    public void NewGame(int? seed = null)
    {
        StopWinAnimation();
        StopAutoMoveAnimation();
        _isDragging = false;
        _draggedCards.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _game.NewGame(seed);
        RenderGame();
        TryAutoMoveToFoundations();
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
