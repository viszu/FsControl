﻿namespace FsControl.Core.Types

open FsControl.Core.Prelude
open FsControl.Core.Abstractions
open FsControl.Core.Abstractions.Monad
open FsControl.Core.Abstractions.MonadPlus
open FsControl.Core.Abstractions.MonadTrans
open FsControl.Core.Abstractions.MonadAsync

type ErrorT<'R> = ErrorT of 'R

[<RequireQualifiedAccess>]
module ErrorT =
    let run   (ErrorT x) = x
    let map f (ErrorT m) = ErrorT(f m)
    let inline internal mapError f = function Choice1Of2 x -> Choice1Of2(f x) | Choice2Of2 x -> Choice2Of2 x

type ErrorT<'R> with
    static member inline instance (Functor.Map, ErrorT x :ErrorT<'ma>, _) = fun (f) -> ErrorT (Functor.fmap (ErrorT.mapError f) x) :ErrorT<'mb>

    static member inline instance (Monad.Return, _:ErrorT<'ma>) = ErrorT << return' << Choice1Of2 :'a -> ErrorT<'ma>
    static member inline instance (Monad.Bind  , ErrorT x :ErrorT<'ma>, _:ErrorT<'mb>) = 
        fun (f: 'a -> ErrorT<'mb>) -> (ErrorT <| do'() {
            let! a = x
            match a with
            | Choice2Of2 l -> return (Choice2Of2 l)
            | Choice1Of2 r -> return! ErrorT.run (f r)}) :ErrorT<'mb>

    static member inline instance (MonadTrans.Lift, _:ErrorT<'m_a>) = ErrorT << (liftM Choice1Of2)      :'ma -> ErrorT<'m_a>

    static member inline instance (MonadError.ThrowError, _:ErrorT<'ma>) = ErrorT << return' << Choice2Of2 :'a -> ErrorT<'ma>
    static member inline instance (MonadError.CatchError, ErrorT x :ErrorT<'ma>, _:ErrorT<'mb>) = 
        fun (f: 'a -> ErrorT<'mb>) -> (ErrorT <| do'() {
            let! a = x
            match a with
            | Choice2Of2 l -> return! ErrorT.run (f l)
            | Choice1Of2 r -> return (Choice1Of2 r)}) :ErrorT<'mb>

    static member inline instance (MonadAsync.LiftAsync, _:ErrorT<'U>) = fun (x :Async<'a>) -> lift (Inline.instance LiftAsync x)

    static member instance (MonadCont.CallCC, _:ErrorT<Cont<'r,Choice<'aa,'bb>>>) = fun (f:((_ -> ErrorT<Cont<_,'b>>) -> _)) -> ErrorT(Cont.callCC <| fun c -> ErrorT.run(f (ErrorT << c << Choice1Of2)))     :ErrorT<Cont<'r,Choice<'aa,'bb>>>

    static member        instance (MonadReader.Ask, _: ErrorT<Reader<'a,Choice<'a,'b>>>) = fun () -> (ErrorT << (liftM Choice1Of2)) (Reader.ask()) : ErrorT<Reader<'a,Choice<'a,'b>>>
    static member inline instance (MonadReader.Local, ErrorT m, _:ErrorT<_> ) = fun f -> ErrorT <| Reader.local f m

    static member inline instance (MonadWriter.Tell, _:ErrorT<_> ) = lift << Writer.tell
    static member inline instance (MonadWriter.Listen, m, _:ErrorT<_> ) = fun () ->
        let liftError (m,w) = ErrorT.mapError (fun x -> (x,w)) m
        ErrorT (Writer.listen (ErrorT.run m) >>= (return' << liftError))
    static member inline instance (MonadWriter.Pass, m, _:ErrorT<_> ) = fun () -> ErrorT (ErrorT.run m >>= maybe (return' None) (liftM Some << Writer.pass << return'))

    static member inline instance (MonadState.Get, _:ErrorT<_>) = fun () -> lift (State.get())
    static member inline instance (MonadState.Put, _:ErrorT<_>) = lift << State.put