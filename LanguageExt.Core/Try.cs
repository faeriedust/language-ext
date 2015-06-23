﻿using System;
using System.Linq;
using System.Collections.Generic;
using LanguageExt;
using static LanguageExt.Prelude;
using System.Collections.Immutable;

namespace LanguageExt
{
    /// <summary>
    /// Try delegate
    /// </summary>
    public delegate TryResult<T> Try<T>();

    /// <summary>
    /// Holds the state of the Try post invocation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct TryResult<T>
    {
        internal readonly T Value;
        internal Exception Exception;

        public TryResult(T value)
        {
            Value = value;
            Exception = null;
        }

        public TryResult(Exception e)
        {
            Exception = e;
            Value = default(T);
        }

        public static implicit operator TryResult<T>(T value) =>
            new TryResult<T>(value);

        internal bool IsFaulted => Exception != null;

        public override string ToString() =>
            IsFaulted
                ? Exception.ToString()
                : Value.ToString();
    }


    public struct TrySuccContext<T, R>
    {
        readonly Try<T> value;
        readonly Func<T, R> succHandler;

        internal TrySuccContext(Try<T> value, Func<T, R> succHandler)
        {
            this.value = value;
            this.succHandler = succHandler;
        }

        public R Fail(Func<Exception, R> failHandler) =>
            value.Match(succHandler, failHandler);

        public R Fail(R failValue) =>
            value.Match(succHandler, _ => failValue);
    }

    public struct TrySuccUnitContext<T>
    {
        readonly Try<T> value;
        readonly Action<T> succHandler;

        internal TrySuccUnitContext(Try<T> value, Action<T> succHandler)
        {
            this.value = value;
            this.succHandler = succHandler;
        }

        public Unit Fail(Action<Exception> failHandler) =>
            value.Match(succHandler, failHandler);
    }

    public static class TryConfig
    {
        public static Action<Exception> ErrorLogger = ex => {};
    }
}

/// <summary>
/// Extension methods for the Try monad
/// </summary>
public static class __TryExt
{
    /// <summary>
    /// Invokes the succHandler if Try is in the Success state, otherwise nothing
    /// happens.
    /// </summary>
    public static Unit IfSucc<T>(this Try<T> self, Action<T> succHandler)
    {
        var res = self.Try();
        if (!res.IsFaulted)
        {
            succHandler(res.Value);
        }
        return unit;
    }

    /// <summary>
    /// Returns the Succ(value) of the Try or a default if it's Fail
    /// </summary>
    public static T IfFail<T>(this Try<T> self, T defaultValue)
    {
        if (defaultValue == null) throw new ArgumentNullException("defaultValue");

        var res = self.Try();
        if (res.IsFaulted)
            return defaultValue;
        else
            return res.Value;
    }

    /// <summary>
    /// Returns the Succ(value) of the Try or a default if it's Fail
    /// </summary>
    public static T IfFail<T>(this Try<T> self, Func<T> defaultAction)
    {
        var res = self.Try();
        if (res.IsFaulted)
            return defaultAction();
        else
            return res.Value;
    }

    public static R Match<T, R>(this Try<T> self, Func<T, R> Succ, Func<Exception, R> Fail)
    {
        var res = self.Try();
        return res.IsFaulted
            ? Fail(res.Exception)
            : Succ(res.Value);
    }

    public static R Match<T, R>(this Try<T> self, Func<T, R> Succ, R Fail)
    {
        if (Fail == null) throw new ArgumentNullException("Fail");

        var res = self.Try();
        return res.IsFaulted
            ? Fail
            : Succ(res.Value);
    }

    public static Unit Match<T>(this Try<T> self, Action<T> Succ, Action<Exception> Fail)
    {
        var res = self.Try();

        if (res.IsFaulted)
            Fail(res.Exception);
        else
            Succ(res.Value);

        return Unit.Default;
    }

    public static Option<T> ToOption<T>(this Try<T> self)
    {
        var res = self.Try();
        return res.IsFaulted
            ? None
            : Optional(res.Value);
    }

    public static TryOption<T> ToTryOption<T>(this Try<T> self) => () =>
    {
        var res = self.Try();
        return res.IsFaulted
            ? None
            : Optional(res.Value);
    };

    public static TryResult<T> Try<T>(this Try<T> self)
    {
        try
        {
            return self();
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return new TryResult<T>(e);
        }
    }

    public static T IfFailThrow<T>(this Try<T> self)
    {
        try
        {
            var res = self();
            if (res.IsFaulted)
            {
                throw res.Exception;
            }
            return res.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            throw;
        }
    }

    public static Try<U> Select<T, U>(this Try<T> self, Func<T, U> select)
    {
        return new Try<U>(() =>
        {
            TryResult<T> resT;
            try
            {
                resT = self();
                if (resT.IsFaulted)
                    return new TryResult<U>(resT.Exception);
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new TryResult<U>(e);
            }

            U resU;
            try
            {
                resU = select(resT.Value);
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new TryResult<U>(e);
            }

            return new TryResult<U>(resU);
        });
    }

    public static Unit Iter<T>(this Try<T> self, Action<T> action) =>
        self.IfSucc(action);

    public static int Count<T>(this Try<T> self)
    {
        var res = self.Try();
        return res.IsFaulted
            ? 0
            : 1;
    }

    public static bool ForAll<T>(this Try<T> self, Func<T, bool> pred)
    {
        var res = self.Try();
        return res.IsFaulted
            ? false
            : pred(res.Value);
    }

    public static S Fold<S, T>(this Try<T> self, S state, Func<S, T, S> folder)
    {
        var res = self.Try();
        return res.IsFaulted
            ? state
            : folder(state, res.Value);
    }

    public static bool Exists<T>(this Try<T> self, Func<T, bool> pred)
    {
        var res = self.Try();
        return res.IsFaulted
            ? false
            : pred(res.Value);
    }

    public static Try<R> Map<T, R>(this Try<T> self, Func<T, R> mapper) => () =>
    {
        var res = self.Try();
        return res.IsFaulted
            ? new TryResult<R>(res.Exception)
            : mapper(res.Value);
    };

    public static Try<T> Filter<T>(this Try<T> self, Func<T, bool> pred)
    {
        var res = self.Try();
        return res.IsFaulted
            ? () => res
            : pred(res.Value)
                ? self
                : () => new TryResult<T>(new Exception("Filtered"));
    }

    public static Try<T> Where<T>(this Try<T> self, Func<T, bool> pred) =>
        self.Filter(pred);

    public static Try<R> Bind<T, R>(this Try<T> self, Func<T, Try<R>> binder) => () =>
    {
        var res = self.Try();
        return !res.IsFaulted
            ? binder(res.Value)()
            : new TryResult<R>(res.Exception);
    };

    public static IEnumerable<Either<Exception, T>> AsEnumerable<T>(this Try<T> self)
    {
        var res = self.Try();

        if (res.IsFaulted)
        {
            yield return res.Exception;
        }
        else
        {
            yield return res.Value;
        }
    }

    public static Lst<Either<Exception, T>> ToList<T>(this Try<T> self) =>
        toList(self.AsEnumerable());

    public static ImmutableArray<Either<Exception, T>> ToArray<T>(this Try<T> self) =>
        toArray(self.AsEnumerable());

    public static TrySuccContext<T, R> Succ<T, R>(this Try<T> self, Func<T, R> succHandler) =>
        new TrySuccContext<T, R>(self, succHandler);

    public static TrySuccUnitContext<T> Succ<T>(this Try<T> self, Action<T> succHandler) =>
        new TrySuccUnitContext<T>(self, succHandler);

    public static int Sum(this Try<int> self) =>
        self.Try().Value;

    public static string AsString<T>(this Try<T> self) =>
        match(self,
            Succ: v => v == null
                      ? "Succ(null)"
                      : String.Format("Succ({0})", v),
            Fail: ex => "Fail(" + ex.Message + ")"
        );


    public static Try<IEnumerable<V>> SelectMany<T, U, V>(
        this Try<T> self,
        Func<T, IEnumerable<U>> bind,
        Func<T, U, V> project
        )
    {
        return new Try<IEnumerable<V>>(() =>
        {
            TryResult<T> resT;
            try
            {
                resT = self();
                if (resT.IsFaulted)
                    return new V[0];
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new V[0];
            }

            IEnumerable<U> resU;
            try
            {
                resU = bind(resT.Value);
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new V[0];
            }

            try
            {
                return new TryResult<IEnumerable<V>>(resU.Select(x => project(resT.Value, x)));
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new V[0];
            }
        });
    }

    public static Try<IEnumerable<V>> SelectMany<T, U, V>(
        this Try<T> self,
        Func<T, Lst<U>> bind,
        Func<T, U, V> project
        )
    {
        return new Try<IEnumerable<V>>(() =>
        {
            TryResult<T> resT;
            try
            {
                resT = self();
                if (resT.IsFaulted)
                    return List<V>();
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return List<V>();
            }

            IEnumerable<U> resU;
            try
            {
                resU = bind(resT.Value);
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return List<V>();
            }

            try
            {
                return LanguageExt.List.createRange(resU.Select(x => project(resT.Value, x)));
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return List<V>();
            }
        });
    }

    public static Try<Map<K, V>> SelectMany<K, T, U, V>(
        this Try<T> self,
        Func<T, Map<K, U>> bind,
        Func<T, U, V> project
        )
    {
        return new Try<LanguageExt.Map<K, V>>(() =>
        {
            TryResult<T> resT;
            try
            {
                resT = self();
                if (resT.IsFaulted)
                    return Prelude.Map<K, V>();
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return Prelude.Map<K, V>();
            }

            Map<K, U> resU;
            try
            {
                resU = bind(resT.Value);
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return Prelude.Map<K, V>();
            }

            try
            {
                return resU.Select(x => project(resT.Value, x));
            }
            catch (Exception e)
            {
                TryConfig.ErrorLogger(e);
                return Prelude.Map<K, V>();
            }
        });
    }

    public static Try<Option<V>> SelectMany<T, U, V>(
          this Try<T> self,
          Func<T, Option<U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<Option<V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<Option<V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Option<V>>(e);
                }

                TryOptionResult<U> resU;
                try
                {
                    resU = bind(resT.Value);
                    if (resU.IsFaulted)
                        return new TryResult<Option<V>>(resU.Exception);
                    if (resU.Value.IsNone)
                        return new TryResult<Option<V>>(None);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Option<V>>(e);
                }

                try
                {
                    return new TryResult<Option<V>>(Prelude.Some(project(resT.Value, resU.Value.Value)));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Option<V>>(e);
                }
            }
        );
    }

    public static Try<OptionUnsafe<V>> SelectMany<T, U, V>(
          this Try<T> self,
          Func<T, OptionUnsafe<U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<OptionUnsafe<V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<OptionUnsafe<V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<OptionUnsafe<V>>(e);
                }

                OptionUnsafe<U> resU;
                try
                {
                    resU = bind(resT.Value);
                    if (resU.IsNone)
                        return new TryResult<OptionUnsafe<V>>(None);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<OptionUnsafe<V>>(e);
                }

                try
                {
                    return new TryResult<OptionUnsafe<V>>(project(resT.Value, resU.Value));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<OptionUnsafe<V>>(e);
                }
            }
        );
    }

    public static Try<V> SelectMany<T, U, V>(
          this Try<T> self,
          Func<T, Try<U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<V>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<V>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<V>(e);
                }

                TryResult<U> resU;
                try
                {
                    resU = bind(resT.Value).Try();
                    if (resU.IsFaulted)
                        return new TryResult<V>(resU.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<V>(e);
                }

                try
                {
                    return new TryResult<V>(project(resT.Value, resU.Value));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<V>(e);
                }
            }
        );
    }

    public static TryOption<V> SelectMany<T, U, V>(
          this Try<T> self,
          Func<T, TryOption<U>> bind,
          Func<T, U, V> project
          )
    {
        return new TryOption<V>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryOptionResult<V>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryOptionResult<V>(e);
                }

                TryOptionResult<U> resU;
                try
                {
                    resU = bind(resT.Value).Try();
                    if (resU.IsFaulted)
                        return new TryOptionResult<V>(resU.Exception);
                    if (resU.Value.IsNone)
                        return new TryOptionResult<V>(None);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryOptionResult<V>(e);
                }

                try
                {
                    return new TryOptionResult<V>(project(resT.Value, resU.Value.Value));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryOptionResult<V>(e);
                }
            }
        );
    }

    public static Try<Either<L, V>> SelectMany<L, T, U, V>(
         this Try<T> self,
         Func<T, Either<L, U>> bind,
         Func<T, U, V> project
         )
    {
        return new Try<Either<L, V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<Either<L, V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Either<L, V>>(e);
                }

                Either<L,U> resU;
                try
                {
                    resU = bind(resT.Value);
                    if (resU.IsLeft)
                        return new TryResult<Either<L, V>>(resU.LeftValue);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Either<L, V>>(e);
                }

                try
                {
                    return new TryResult<Either<L, V>>(Right<L, V>(project(resT.Value, resU.RightValue)));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Either<L, V>>(e);
                }

            }
        );
    }

    public static Try<EitherUnsafe<L, V>> SelectMany<L, T, U, V>(
          this Try<T> self,
          Func<T, EitherUnsafe<L, U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<EitherUnsafe<L, V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<EitherUnsafe<L, V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<EitherUnsafe<L, V>>(e);
                }

                EitherUnsafe<L, U> resU;
                try
                {
                    resU = bind(resT.Value);
                    if (resU.IsLeft)
                        return new TryResult<EitherUnsafe<L, V>>(resU.LeftValue);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<EitherUnsafe<L, V>>(e);
                }

                try
                {
                    return new TryResult<EitherUnsafe<L, V>>(RightUnsafe<L, V>(project(resT.Value, resU.RightValue)));
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<EitherUnsafe<L, V>>(e);
                }

            }
        );
    }


    public static Try<Reader<E, V>> SelectMany<E, T, U, V>(
          this Try<T> self,
          Func<T, Reader<E, U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<Reader<E, V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<Reader<E, V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Reader<E, V>>(e);
                }

                return new TryResult<LanguageExt.Reader<E, V>>(env =>
                {
                    var resU = bind(resT.Value)(env);
                    if (resU.IsBottom)
                    {
                        return new ReaderResult<V>(default(V), true);
                    }
                    return new ReaderResult<V>(project(resT.Value, resU.Value));
                });
            }
        );
    }

    public static Try<Writer<Out, V>> SelectMany<Out, T, U, V>(
          this Try<T> self,
          Func<T, Writer<Out, U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<Writer<Out, V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<Writer<Out, V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<Writer<Out, V>>(e);
                }

                return new TryResult<Writer<Out, V>>(() =>
                {
                    WriterResult<Out, U> resU = bind(resT.Value)();
                    if (resU.IsBottom)
                    {
                        return new WriterResult<Out, V>(default(V), resU.Output, true);
                    }
                    return new WriterResult<Out, V>(project(resT.Value, resU.Value), resU.Output);
                });
            }
        );
    }

    public static Try<State<S, V>> SelectMany<S, T, U, V>(
          this Try<T> self,
          Func<T, State<S, U>> bind,
          Func<T, U, V> project
          )
    {
        return new Try<State<S, V>>(
            () =>
            {
                TryResult<T> resT;
                try
                {
                    resT = self();
                    if (resT.IsFaulted)
                        return new TryResult<State<S, V>>(resT.Exception);
                }
                catch (Exception e)
                {
                    TryConfig.ErrorLogger(e);
                    return new TryResult<State<S, V>>(e);
                }

                return new TryResult<State<S, V>>((S state) =>
                {
                    StateResult<S, U> resU = bind(resT.Value)(state);
                    if (resU.IsBottom)
                    {
                        return new StateResult<S, V>(resU.State, default(V), true);
                    }
                    return new StateResult<S, V>(resU.State, project(resT.Value, resU.Value));
                });
            }
        );
    }
}