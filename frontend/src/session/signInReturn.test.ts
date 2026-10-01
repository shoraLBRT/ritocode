import { describe, expect, it } from 'vitest';
import { checkReturnPath, readSignInReturn, withoutSignInReturn } from './signInReturn';

describe('the return from a sign-in', () => {
  it('asks the page that sent the learner to check', () => {
    expect(checkReturnPath('/tasks/a')).toBe('/tasks/a?check=1');
    expect(readSignInReturn('/tasks/a', '?check=1')).toEqual({ error: null, checkRequestedAt: '/tasks/a' });
  });

  it('checks nothing when the sign-in failed', () => {
    expect(readSignInReturn('/tasks/a', '?check=1&signInError=email_unverified')).toEqual({
      error: 'email_unverified',
      checkRequestedAt: null,
    });
  });

  it('is nothing on an ordinary visit', () => {
    expect(readSignInReturn('/tasks/a', '?q=x')).toEqual({ error: null, checkRequestedAt: null });
  });

  it('leaves the rest of the address alone when taken out of it', () => {
    expect(withoutSignInReturn('?q=x&check=1&signInError=provider_failed')).toBe('?q=x');
    expect(withoutSignInReturn('?check=1')).toBe('');
  });
});
