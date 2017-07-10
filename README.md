# Kami 2 Solver in F#

[Kami 2](http://www.stateofplaygames.com/kami2/) is a beautiful puzzle game
based on coloring oragami paper. The best way is to see how the gamelay
_unfolds_ is to just watch a [quick demo](https://youtu.be/I0S9rnqa5tQ?t=2m23s).

This repo contains a solver for Kami 2 puzzels written in F#. It takes a
screenshot of the puzzle as input, and outputs a sequence of images to
perform to solve the puzzle.

Details on the algorithm and approach can be found on my blog at:
[completely-unique.com](http://completely-unique.com).

- [Part 1](http://completely-unique.com/posts/kami2-solver-part-1)
- [Part 2](http://completely-unique.com/posts/kami2-solver-part-2)

## Known Issues

- "Textured" colors are not supported. (Only seen in some user-generated
  puzzles.)
- It cannot handle puzzles with 10+ moves. In theory it will find a
  solution eventually, but obviously a better algorithm is needed.

## License

Source code is made available under the [MIT license](LICENSE).

_Kami 2_ and related imagery are copyright [_State of Play_](http://www.stateofplaygames.com) games.
